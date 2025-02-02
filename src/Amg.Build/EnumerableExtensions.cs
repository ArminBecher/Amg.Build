﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Amg.Build
{
    /// <summary>
    /// Extensions for IEnumerable
    /// </summary>
    public static class EnumerableExtensions
    {
        private static readonly Serilog.ILogger Logger = Serilog.Log.Logger.ForContext(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Concat one (1) new element
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="e"></param>
        /// <param name="newElement"></param>
        /// <returns></returns>
        public static IEnumerable<T> Concat<T>(this IEnumerable<T> e, T newElement)
        {
            return e.Concat(Enumerable.Repeat(newElement, 1));
        }

        /// <summary>
        /// Convert to strings and concatenate with separator
        /// </summary>
        /// <param name="e"></param>
        /// <param name="separator"></param>
        /// <returns></returns>
        public static string Join(this IEnumerable<object> e, string separator)
        {
            return string.Join(separator, e.Where(_ => _ != null));
        }

        /// <summary>
        /// Convert to strings and concatenate with newline
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public static string Join(this IEnumerable<object> e)
        {
            return e.Join(System.Environment.NewLine);
        }

        /// <summary>
        /// Split a string into lines
        /// </summary>
        /// <param name="multiLineString"></param>
        /// <returns></returns>
        public static IEnumerable<string> SplitLines(this string multiLineString)
        {
            using (var r = new StringReader(multiLineString))
            {
                while (true)
                {
                    var line = r.ReadLine();
                    if (line == null)
                    {
                        break;
                    }
                    yield return line;
                }
            }
        }

        /// <summary>
        /// Zips together two sequences. The shorter sequence is padded with default values.
        /// </summary>
        /// <typeparam name="TFirst"></typeparam>
        /// <typeparam name="TSecond"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <param name="resultSelector"></param>
        /// <returns></returns>
        public static IEnumerable<TResult> ZipOrDefault<TFirst, TSecond, TResult>(this IEnumerable<TFirst> first, IEnumerable<TSecond> second, Func<TFirst, TSecond, TResult> resultSelector)
        {
            using (var i0 = first.GetEnumerator())
            using (var i1 = second.GetEnumerator())
            {
                while (true)
                {
                    var firstHasElement = i0.MoveNext();
                    var secondHasElement = i1.MoveNext();
                    if (firstHasElement || secondHasElement)
                    {
                        yield return resultSelector(
                            firstHasElement ? i0.Current : default(TFirst),
                            secondHasElement ? i1.Current : default(TSecond)
                            );
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Returns the element i for which selector(i) is maximal
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="Y"></typeparam>
        /// <param name="e"></param>
        /// <param name="selector"></param>
        /// <returns></returns>
        public static IEnumerable<T> MaxElement<T, Y>(this IEnumerable<T> e, Func<T, Y> selector) where Y : IComparable
        {
            using (var i = e.GetEnumerator())
            {
                T max = default(T);
                Y maxValue = default(Y);
                bool found = false;

                for (; i.MoveNext();)
                {
                    max = i.Current;
                    maxValue = selector(i.Current);
                    found = true;
                    break;
                }
                for (; i.MoveNext();)
                {
                    var value = selector(i.Current);
                    if (value.CompareTo(maxValue) == 1)
                    {
                        maxValue = value;
                        max = i.Current;
                    }
                }
                return found
                    ? new[] { max }
                    : Enumerable.Empty<T>();
            }
        }

        /// <summary>
        /// Find an element by name. The name of an element i is determined by name(i). 
        /// </summary>
        /// Abbreviations are allowed: query can also be a substring of the name as long as it uniquely
        /// identifies an element.
        /// <typeparam name="T"></typeparam>
        /// <param name="candidates"></param>
        /// <param name="name">calculates the name of an element</param>
        /// <param name="query">the name (part) to be found.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException">When query does not identify a named element.</exception>
        public static T FindByNameOrDefault<T>(this IEnumerable<T> candidates, Func<T, string> name, string query)
        {
            try
            {
                return candidates.FindByName(name, query);
            }
            catch (Exception)
            {
                return default(T);
            }
        }

        /// <summary>
        /// Find an element by name. The name of an element i is determined by name(i). 
        /// </summary>
        /// Abbreviations are allowed: query can also be a substring of the name as long as it uniquely
        /// identifies an element.
        /// <typeparam name="T"></typeparam>
        /// <param name="candidates"></param>
        /// <param name="name">calculates the name of an element</param>
        /// <param name="query">the name (part) to be found.</param>
        /// <param name="itemsName"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException">When query does not identify a named element.</exception>
        public static T FindByName<T>(this IEnumerable<T> candidates, Func<T, string> name, string query,
            string itemsName = null)
        {
            var r = candidates.SingleOrDefault(option =>
                name(option).Equals(query, StringComparison.InvariantCultureIgnoreCase));

            if (r != null)
            {
                return r;
            }

            var matches = candidates.Where(option => query.IsAbbreviation(name(option)))
                .ToList();

            if (matches.Count > 1)
            {
                throw new ArgumentOutOfRangeException($@"{query.Quote()} is ambiguous. Could be

{matches.Select(name).Join()}

");
            }

            if (matches.Count == 1)
            {
                return matches[0];
            }

            throw new ArgumentOutOfRangeException(nameof(query), query, $@"{query.Quote()} not found in {itemsName}

{candidates.Select(_ => "  " + name(_)).Join()}
");
        }

        static IProgress<T> ToProgress<T>(Action<T> a)
        {
            return new ProgressAction<T>(a);
        }

        /// <summary>
        /// Shows progress information is enumerating takes longer
        /// </summary>
        /// <param name="e"></param>
        /// <param name="metric"></param>
        /// <param name="updateInterval"></param>
        /// <param name="metricUnit"></param>
        /// <param name="description"></param>
        /// <returns></returns>
        public static IEnumerable<T> Progress<T>(this IEnumerable<T> e, 
            Func<T, double> metric = null, 
            TimeSpan updateInterval = default(TimeSpan),
            string metricUnit = null,
            string description = null
            )
        {
            if (description == null)
            {
                description = String.Empty;
            }
            else
            {
                description = description + " ";
            }
            if (metric == null)
            {
                metric = (item) => 1.0;
            }
            if (updateInterval == default(TimeSpan))
            {
                updateInterval = TimeSpan.FromSeconds(2);
            }
            if (metricUnit == null)
            {
                metricUnit = String.Empty;
            }
            Func<double, string> format = (double x) => x.MetricShort();

            var speedUnit = $"{metricUnit}/s";

            var progress = ToProgress((ProgressUpdate<T> p) =>
            {
                Logger.Information("{description}{total}{unit} complete, {speed}{speedUnit}: {item}",
                    description,
                    format(p.Total),
                    metricUnit,
                    format(p.Speed),
                    speedUnit, p.Current);
            });

            return e.Progress(progress,
                metric: metric,
                updateInterval: updateInterval
                );
        }

        /// <summary>
        /// Progress when iterating an IEnumerable T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public class ProgressUpdate<T>
        {
            /// <summary />
            public ProgressUpdate(T current, TimeSpan elapsed, double total, double speed)
            {
                this.Current = current;
                Elapsed = elapsed;
                Total = total;
                Speed = speed;
            }

            /// <summary>
            /// Current item of the enumeration
            /// </summary>
            public T Current { get; }

            /// <summary>
            /// Total metric
            /// </summary>
            public double Total { get; }

            /// <summary>
            /// metric Speed (windowed)
            /// </summary>
            public double Speed { get; }

            /// <summary>
            /// Total elapsed time since start of the enumeration
            /// </summary>
            public TimeSpan Elapsed { get; }
        }

        /// <summary>
        /// Shows progress information is enumerating takes longer
        /// </summary>
        /// <param name="e"></param>
        /// <param name="progress"></param>
        /// <param name="metric"></param>
        /// <param name="updateInterval"></param>
        /// <returns></returns>
        public static IEnumerable<T> Progress<T>(this IEnumerable<T> e,
            IProgress<ProgressUpdate<T>> progress,
            Func<T, double> metric = null,
            TimeSpan updateInterval = default(TimeSpan)
            )
        {
            if (metric == null)
            {
                metric = (item) => 1.0;
            }
            if (updateInterval == default(TimeSpan))
            {
                updateInterval = TimeSpan.FromSeconds(2);
            }
            Func<double, string> format = (double x) => x.MetricShort();

            double total = 0.0;
            TimeSpan nextUpdate = TimeSpan.FromTicks(updateInterval.Ticks * 5);
            var stopwatch = Stopwatch.StartNew();
            var speedWindow = TimeSpan.FromTicks(updateInterval.Ticks * 5);
            double delta = 0.0;
            double speed = 0.0;
            TimeSpan lastUpdate = TimeSpan.Zero;

            return e.Select(_ =>
            {
                delta += metric(_);
                var elapsed = stopwatch.Elapsed;
                if (elapsed > nextUpdate)
                {
                    var deltaT = elapsed - lastUpdate;
                    total += delta;
                    speed = (speed * speedWindow.TotalSeconds + delta / deltaT.TotalSeconds) / (speedWindow.TotalSeconds + deltaT.TotalSeconds);
                    progress.Report(new ProgressUpdate<T>(_, elapsed, total, speed));

                    nextUpdate = stopwatch.Elapsed + updateInterval;
                    delta = 0.0;
                    lastUpdate = elapsed;
                }

                return _;
            });
        }
    }
}
