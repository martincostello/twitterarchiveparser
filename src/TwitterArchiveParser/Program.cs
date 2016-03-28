// Copyright (c) Martin Costello, 2016. All rights reserved.
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
                string archivePath = args.FirstOrDefault() ?? Environment.CurrentDirectory;

                var user = ReadUserFromArchive(archivePath);
                var tweets = ReadTweetsFromArchive(archivePath);

                ProcessTweets(user, tweets);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            Console.Write("Press any key to exit...");
            Console.ReadKey();
        }

        /// <summary>
        /// Builds the data path from the specified archive path and sub-directories.
        /// </summary>
        /// <param name="archivePath">The path of the directory containing the archive data.</param>
        /// <param name="paths">The optional sub-directories to get the data path for.</param>
        /// <returns>
        /// A <see cref="string"/> containing the full path for the specified data directory.
        /// </returns>
        private static string BuildDataPath(string archivePath, params string[] paths)
        {
            string dataPath = Path.Combine(archivePath, ConfigurationManager.AppSettings["ArchiveDataPath"]);
            string directoryPath = Path.Combine(paths);

            string path = Path.Combine(dataPath, directoryPath);

            return Path.GetFullPath(path);
        }

        /// <summary>
        /// Reads the raw JSON from the specified <c>JavaScript</c> file.
        /// </summary>
        /// <param name="fileName">The path to the <c>JavaScript</c> file containing JSON.</param>
        /// <returns>
        /// A <see cref="string"/> containing the JSON in the specified file.
        /// </returns>
        private static string ReadJsonFromFile(string fileName)
        {
            string[] lines = File.ReadAllLines(fileName, Encoding.UTF8);

            // Skip the header
            return string.Join(Environment.NewLine, lines.Skip(1));
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
            string tweetsPath = BuildDataPath(archivePath, "tweets");

            var dataFiles = Directory.EnumerateFiles(tweetsPath, "*.js").OrderBy((p) => p);

            var timeline = Enumerable.Empty<JObject>();

            foreach (string fileName in dataFiles)
            {
                var tweets = ReadTweetsFromFile(fileName);
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
        private static IEnumerable<JObject> ReadTweetsFromFile(string fileName)
        {
            string json = ReadJsonFromFile(fileName);

            var tweets = JArray.Parse(json);

            return tweets
                .OfType<JObject>()
                .ToArray();
        }

        /// <summary>
        /// Reads the user data from the specified Twitter archive directory.
        /// </summary>
        /// <param name="archivePath">The path to the directory containing the Twitter archive.</param>
        /// <returns>
        /// A <see cref="JObject"/> containing the user read from the specified Twitter archive.
        /// </returns>
        private static JObject ReadUserFromArchive(string archivePath)
        {
            string userPath = BuildDataPath(archivePath);
            string fileName = Path.Combine(userPath, "user_details.js");

            string json = ReadJsonFromFile(fileName);

            const string JObjectPrefix = "{";

            if (!json.StartsWith(JObjectPrefix, StringComparison.Ordinal))
            {
                json = JObjectPrefix + json;
            }

            return JObject.Parse(json);
        }

        /// <summary>
        /// Processes the specified user's tweets.
        /// </summary>
        /// <param name="user">The user the tweets belong to.</param>
        /// <param name="tweets">The tweets to process.</param>
        private static void ProcessTweets(JObject user, IEnumerable<JObject> tweets)
        {
            var orderedTweets = tweets
                .OrderBy((p) => p["id"])
                .ToArray();

            UserDetails(user);
            CountTweets(orderedTweets);
            Top10Mentions(orderedTweets);
            Top10Hashtags(orderedTweets);
        }

        /// <summary>
        /// Prints the number of tweets in the specified collection.
        /// </summary>
        /// <param name="tweets">The tweets to count.</param>
        private static void CountTweets(IEnumerable<JObject> tweets)
        {
            Console.WriteLine($"This Twitter archive contains {tweets.Count():N0} tweets.");
            Console.WriteLine();
        }

        /// <summary>
        /// Prints up to the top 10 most used hashtags in the specified tweets.
        /// </summary>
        /// <param name="tweets">The tweets to get up to the top 10 hashtags for.</param>
        private static void Top10Hashtags(IEnumerable<JObject> tweets)
        {
            var hashtags = tweets
                .SelectMany((p) => p["entities"]?["hashtags"]?.OfType<JObject>())
                .GroupBy((p) => ((string)p["text"]).ToLowerInvariant())
                .Select((p) => new { screen_name = p.Key, count = p.Count() })
                .OrderByDescending((p) => p.count)
                .Take(10)
                .ToArray();

            Console.WriteLine($"Top {hashtags.Length} hashtags:");
            Console.WriteLine();

            for (int i = 0; i < hashtags.Length; i++)
            {
                var hashtag = hashtags[i];
                Console.WriteLine($"  {i + 1, 2:N0}. #{hashtag.screen_name} ({hashtag.count:N0})");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Prints up to the top 10 most mentioned users in the specified tweets.
        /// </summary>
        /// <param name="tweets">The tweets to get up to the top 10 mentions for.</param>
        private static void Top10Mentions(IEnumerable<JObject> tweets)
        {
            var interactions = tweets
                .SelectMany((p) => p["entities"]?["user_mentions"]?.OfType<JObject>())
                .GroupBy((p) => p["screen_name"])
                .Select((p) => new { screen_name = p.Key, count = p.Count() })
                .OrderByDescending((p) => p.count)
                .Take(10)
                .ToArray();

            Console.WriteLine($"Top {interactions.Length} mentioned users:");
            Console.WriteLine();

            for (int i = 0; i < interactions.Length; i++)
            {
                var user = interactions[i];
                Console.WriteLine($"  {i + 1, 2:N0}. @{user.screen_name} ({user.count:N0})");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Prints details about the specified Twitter user.
        /// </summary>
        /// <param name="user">The Twitter user to print the details for.</param>
        private static void UserDetails(JObject user)
        {
            Console.WriteLine(
                $@"Screen name: @{user["screen_name"]}
         Id: {user["id"]}
  Full name: {user["full_name" ?? "?"]}
Date joined: {user["created_at"]},
   Location: {user["location"] ?? "?"}");

            Console.WriteLine();
        }

        /// <summary>
        /// Prints a banner to the console containing application information.
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
