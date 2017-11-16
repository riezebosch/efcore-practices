using EFCore.Practices.Tests.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EFCore.Practices.Tests
{
    public class SelectPlusOneVerifierTests
    {
        private static DbContextOptions<SchoolContext> Options
        {
            get
            {
                return Builder.Options;
            }
        }

        private static DbContextOptionsBuilder<SchoolContext> Builder
        {
            get
            {
                return new DbContextOptionsBuilder<SchoolContext>()
                    .UseSqlServer(@"Server=.\SQLEXPRESS;Database=School;Trusted_Connection=True;");
            }
        }

        /// <summary>
        /// Workaround conform issue: https://github.com/aspnet/EntityFramework/issues/4007#issuecomment-173297373
        /// </summary>
        [Fact]
        public void IncludeNotIgnoredWhenProjectionAfterQueryExecution()
        {
            var options = Builder
                .ConfigureWarnings(w => w.Throw(CoreEventId.IncludeIgnoredWarning))
                .Options;

            using (var context = new SchoolContext(options))
            using (var provider = new SelectPlusOneLoggerProvider(context))
            {
                var result = context
                    .Person
                    .Include(p => p.StudentGrade)
                        .ThenInclude(pc => pc.Course)
                    .ToList() // force execution, loading additional data, projecting later on
                    .Select(p => new
                    {
                        p.FirstName,
                        p.LastName,
                        Courses = p.StudentGrade.Select(sg => sg.Course.Title)
                    }).ToList();

                Assert.NotEmpty(result);
                Assert.Contains(result, p => p.Courses.Any());

                provider.Verify();
            }
        }

        /// <summary>
        /// Conforming documentation: https://docs.microsoft.com/en-us/ef/core/querying/related-data#ignored-includes
        /// </summary>
        [Fact]
        public void IncludeIgnoredWhenEntityNotUsedInProjection()
        {
            var options = Builder
                .ConfigureWarnings(w => w.Throw(CoreEventId.IncludeIgnoredWarning))
                .Options;

            using (var context = new SchoolContext(options))
            {
                var query = context
                    .Person
                    .Include(p => p.StudentGrade)
                        .ThenInclude(pc => pc.Course)
                    .Select(p => new
                    {
                        p.FirstName,
                        p.LastName,
                        Courses = p.StudentGrade.Select(sg => sg.Course.Title)
                    });

                var ex = Assert.Throws<InvalidOperationException>(() => query.ToList());
                Assert.Contains("StudentGrade.Course", ex.Message);
            }
        }

        [Fact]
        public void OneToOne()
        {
            using (var context = new SchoolContext(Options))
            using (var provider = new SelectPlusOneLoggerProvider(context, treshold: 2))
            {
                var result = context
                    .Person
                    .Select(p => new
                    {
                        p.FirstName,
                        p.LastName,
                        p.OfficeAssignment.Location
                    }).ToList();

                Assert.NotEmpty(result);
                Assert.Contains(result, p => p.Location != null);

                provider.Verify();
            }
        }

        [Fact]
        public void OneToMany()
        {
            using (var context = new SchoolContext(Options))
            using (var provider = new SelectPlusOneLoggerProvider(context, treshold: 2))
            {
                var result = context
                    .Person
                    .Select(p => new
                    {
                        p.FirstName,
                        p.LastName,
                        Grades = p.StudentGrade.Select(sg => sg.Grade)
                    }).ToList();

                Assert.NotEmpty(result);
                foreach (var p in result)
                {
                    foreach (var g in p.Grades)
                    {
                    }
                }

                var ex = Assert.Throws<AggregateException>(() => provider.Verify());
                Assert.IsType<PossibleSlectPlusOneQueryException>(ex.InnerException);
            }
        }

        [Fact]
        public void OneToManyNavigationPropertyPartOfSelect()
        {
            using (var context = new SchoolContext(Options))
            using (var provider = new SelectPlusOneLoggerProvider(context, treshold: 2))
            {
                var result = context
                    .Person
                    .Select(p => new
                    {
                        p.FirstName,
                        p.LastName,
                        Grades = p.StudentGrade
                    }).ToList();

                Assert.NotEmpty(result);
                foreach (var p in result)
                {
                    foreach (var g in p.Grades)
                    {
                    }
                }

                var ex = Assert.Throws<AggregateException>(() => provider.Verify());
                Assert.IsType<PossibleSlectPlusOneQueryException>(ex.InnerException);
            }
        }

        [Fact]
        public void ManyToMany()
        {
            using (var context = new SchoolContext(Options))
            using (var provider = new SelectPlusOneLoggerProvider(context, treshold: 2))
            {
               

                // Arrange
                var result = context
                    .Person
                    .Select(p => new
                    {
                        p.FirstName,
                        p.LastName,
                        Courses = p.StudentGrade.Select(sg => sg.Course.Title)
                    }).ToList();

                // Act
                Assert.NotEmpty(result);
                foreach (var p in result)
                {
                    foreach (var c in p.Courses)
                    {
                    }
                }

                // Assert
                var ex = Assert.Throws<AggregateException>(() => provider.Verify());
                Assert.IsType<PossibleSlectPlusOneQueryException>(ex.InnerException);
            }
        }

        [Fact]
        public void LoggerProviderThrowsExceptionForRepeatingQueriesOverTreshold()
        {
            // Arrange 
            var provider = InitializeLoggerProviderForTesting();

            // Act
            var ex = Assert.Throws<AggregateException>(() => provider.Verify());
            Assert.IsType<PossibleSlectPlusOneQueryException>(ex.InnerException);
        }

        [Fact]
        public void LoggerProviderThrowsExceptionsOnDisposeWhenNotVerified()
        {
            // Arrange 
            var provider = InitializeLoggerProviderForTesting();

            // Act
            var ex = Assert.Throws<NotVerifiedException>(() => provider.Dispose());
            Assert.IsType<AggregateException>(ex.InnerException);
        }

        [Fact]
        public void LoggerProviderDoesNotThrowExceptionsOnDisposeWhenVerified()
        {
            // Arrange 
            var provider = InitializeLoggerProviderForTesting();
            try
            {
                provider.Verify();
            }
            catch (AggregateException)
            {
            }

            // Act
            provider.Dispose();
        }

        private static SelectPlusOneLoggerProvider InitializeLoggerProviderForTesting(int count = 20, int treshold = 20)
        {
            var provider = new SelectPlusOneLoggerProvider(treshold);
            var logger = provider.CreateLogger(null);

            for (int i = 0; i < count; i++)
            {
                logger.Log<object>(LogLevel.Critical, new EventId(i), null, null, null);
            }

            return provider;
        }
    }
}
