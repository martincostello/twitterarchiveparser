﻿// Copyright (c) Martin Costello, 2016. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.TwitterArchiveParser
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// A console application that parses the JSON of Twitter archives. This class cannot be inherited.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// The main entry point to the application.
        /// </summary>
        /// <param name="args">The command-line arguments passed to the application.</param>
        internal static void Main(string[] args)
        {
            PrintBanner();

            try
            {
                var tweets = ReadTweetsFromArchive(archivePath: args.FirstOrDefault() ?? Environment.CurrentDirectory);

                ProcessTweets(tweets);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            Console.WriteLine();
            Console.Write("Press any key to exit...");
            Console.ReadKey();
        }

        /// <summary>
        /// Reads the tweet data from the specified Twitter archive directory.
        /// </summary>
        /// <param name="archivePath">The path to the directory containing the Twitter archive.</param>
        /// <returns>
        /// A <see cref="IEnumerable{T}"/> containing the tweets read from the specified Twitter archive.
        /// </returns>
        private static IEnumerable<JObject> ReadTweetsFromArchive(string archivePath)
        {
            string dataPath = Path.Combine(archivePath, ConfigurationManager.AppSettings["ArchiveDataPath"]);

            dataPath = Path.GetFullPath(dataPath);

            var dataFiles = Directory.EnumerateFiles(dataPath, "*.js").OrderBy((p) => p);

            var timeline = Enumerable.Empty<JObject>();

            foreach (string fileName in dataFiles)
            {
                var tweets = ReadTweetsFromJavaScript(fileName);
                timeline = timeline.Concat(tweets);
            }

            return timeline;
        }

        /// <summary>
        /// Reads tweet JSON from the specified <c>JavaScript</c> file.
        /// </summary>
        /// <param name="fileName">The path to the <c>JavaScript</c> file containing tweet data.</param>
        /// <returns>
        /// An <see cref="IEnumerable{T}"/> containing the tweets in the specified file.
        /// </returns>
        private static IEnumerable<JObject> ReadTweetsFromJavaScript(string fileName)
        {
            string[] lines = File.ReadAllLines(fileName, Encoding.UTF8);

            // Skip the header
            string json = string.Join(Environment.NewLine, lines.Skip(1));

            var tweets = JArray.Parse(json);

            return tweets
                .OfType<JObject>()
                .ToArray();
        }

        /// <summary>
        /// Processes the specified tweets.
        /// </summary>
        /// <param name="tweets">The tweets to process.</param>
        private static void ProcessTweets(IEnumerable<JObject> tweets)
        {
            var orderedTweets = tweets
                .OrderBy((p) => p["id"])
                .ToArray();

            Console.WriteLine($"The Twitter archive contains {orderedTweets.Count():N0} tweets.");
        }

        /// <summary>
        /// Prints a banner to the console containing assembly and operating system information.
        /// </summary>
        private static void PrintBanner()
        {
            Assembly assembly = typeof(Program).Assembly;
            AssemblyName assemblyName = assembly.GetName();

            Console.WriteLine(
                $@"Twitter Archive Parser - v{assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion}
{assembly.GetCustomAttribute<AssemblyCopyrightAttribute>().Copyright}
");
        }
    }
}
