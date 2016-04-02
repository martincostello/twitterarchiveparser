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
    using System.Threading.Tasks;
    using MongoDB.Bson;
    using MongoDB.Driver;
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
            MainAsync(args).GetAwaiter().GetResult();
        }

        /// <summary>
        /// The main asynchronous entry point to the application.
        /// </summary>
        /// <param name="args">The command-line arguments passed to the application.</param>
        /// <returns>
        /// A <see cref="Task"/> representing the application's execution.
        /// </returns>
        private static async Task MainAsync(string[] args)
        {
            PrintBanner();

            try
            {
                string archivePath = args.FirstOrDefault() ?? Environment.CurrentDirectory;

                if (string.Equals(archivePath, "--import-to-mongodb", StringComparison.OrdinalIgnoreCase))
                {
                    archivePath = args.ElementAtOrDefault(1) ?? Environment.CurrentDirectory;
                    await ImportTweetsAsync(archivePath);
                }
                else
                {
                    ProcessTweets(archivePath);
                }
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
        /// Prints the number of tweets in the specified collection that were geo-tagged.
        /// </summary>
        /// <param name="tweets">The tweets to count for geo-tags.</param>
        private static void CountGeotaggedTweets(IEnumerable<JObject> tweets)
        {
            var geotags = tweets
                .Select((p) => p["geo"]?["coordinates"])
                .Where((p) => p != null)
                .ToArray();

            Console.WriteLine($"This Twitter archive contains {geotags.Length:N0} geo-tagged tweets.");
            Console.WriteLine();
        }

        /// <summary>
        /// Prints the number of media items embedded in the tweets in the specified collection.
        /// </summary>
        /// <param name="tweets">The tweets to count the media in.</param>
        private static void CountMedia(IEnumerable<JObject> tweets)
        {
            var media = tweets
                .SelectMany((p) => p["entities"]?["media"]?.OfType<JObject>())
                .ToArray();

            Console.WriteLine($"This Twitter archive contains {media.Length:N0} items of media.");
            Console.WriteLine();

            var mediaTypes = media
                .GroupBy((p) => ((string)p["media_url"]).Split('.').Last().ToUpperInvariant())
                .Select((p) => new { type = p.Key, count = p.Count() })
                .OrderByDescending((p) => p.count)
                .Take(10)
                .ToArray();

            Console.WriteLine($"Top {mediaTypes.Length} media types:");
            Console.WriteLine();

            for (int i = 0; i < mediaTypes.Length; i++)
            {
                var mediaType = mediaTypes[i];
                Console.WriteLine($"  {i + 1, 2:N0}. {mediaType.type} ({mediaType.count:N0})");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Prints the number of retweets in the tweets in the specified collection.
        /// </summary>
        /// <param name="tweets">The tweets to count the retweets in.</param>
        private static void CountRetweets(IEnumerable<JObject> tweets)
        {
            var retweets = tweets
                .Select((p) => p["retweeted_status"])
                .Where((p) => p != null)
                .ToArray();

            Console.WriteLine($"This Twitter archive contains {retweets.Length:N0} retweets.");
            Console.WriteLine();

            var retweetedUsers = retweets
                .GroupBy((p) => p["user"]?["screen_name"])
                .Select((p) => new { screen_name = p.Key, count = p.Count() })
                .OrderByDescending((p) => p.count)
                .Take(10)
                .ToArray();

            Console.WriteLine($"Top {retweetedUsers.Length} retweeted users:");
            Console.WriteLine();

            for (int i = 0; i < retweetedUsers.Length; i++)
            {
                var mediaType = retweetedUsers[i];
                Console.WriteLine($"  {i + 1, 2:N0}. {mediaType.screen_name} ({mediaType.count:N0})");
            }

            Console.WriteLine();
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
        /// Displays the user's first tweet.
        /// </summary>
        /// <param name="tweet">The first tweet.</param>
        private static void FirstTweet(JObject tweet)
        {
            Console.WriteLine("First tweet:");
            Console.WriteLine();

            Console.WriteLine(
                $@"  Posted at: {tweet["created_at"]}

  Text: ""{tweet["text"]}"" ({((string)tweet["text"]).Length} characters)
");
        }

        /// <summary>
        /// Loads a list of common English words.
        /// </summary>
        /// <returns>
        /// An <see cref="IEnumerable{T}"/> containing common English words.
        /// </returns>
        private static IEnumerable<string> LoadCommonEnglishWords()
        {
            var type = typeof(Program).GetTypeInfo();

            using (var stream = type.Assembly.GetManifestResourceStream($"{type.Namespace}.common-english-words.txt"))
            {
                using (var reader = new StreamReader(stream))
                {
                    while (!reader.EndOfStream)
                    {
                        yield return reader.ReadLine();
                    }
                }
            }
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
        /// Processes the tweets from the specified Twitter archive directory.
        /// </summary>
        /// <param name="archivePath">The path to the directory containing the Twitter archive.</param>
        private static void ProcessTweets(string archivePath)
        {
            var user = ReadUserFromArchive(archivePath);
            var tweets = ReadTweetsFromArchive(archivePath);

            ProcessTweets(user, tweets);
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
            CountRetweets(orderedTweets);
            CountMedia(orderedTweets);
            CountGeotaggedTweets(orderedTweets);
            Top10Mentions(orderedTweets);
            Top10Hashtags(orderedTweets);
            Top10Words(orderedTweets);
            FirstTweet(orderedTweets.First());
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
                .Select((p) => new { text = p.Key, count = p.Count() })
                .OrderByDescending((p) => p.count)
                .Take(10)
                .ToArray();

            Console.WriteLine($"Top {hashtags.Length} hashtags:");
            Console.WriteLine();

            for (int i = 0; i < hashtags.Length; i++)
            {
                var hashtag = hashtags[i];
                Console.WriteLine($"  {i + 1, 2:N0}. #{hashtag.text} ({hashtag.count:N0})");
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
        /// Prints up to the top 10 most used words in the specified tweets.
        /// </summary>
        /// <param name="tweets">The tweets to get up to the top 10 words for.</param>
        private static void Top10Words(IEnumerable<JObject> tweets)
        {
            var punctuation = new[] { '.', ',', '!', '?', '£', '$', '\"', '\'', '*', ';', ':', '(', ')', '&', '-' };

            var words = tweets
                .Select((p) => (string)p["text"])
                .SelectMany((p) => p.Split(' '))
                .Select((p) => p.Trim(punctuation))
                .Where((p) => !p.StartsWith("#"))
                .Where((p) => !p.StartsWith("@"))
                .Except(LoadCommonEnglishWords())
                .GroupBy((p) => p.ToLowerInvariant())
                .Select((p) => new { text = p.Key, count = p.Count() })
                .OrderByDescending((p) => p.count)
                .ThenBy((p) => p.text)
                .Take(10)
                .ToArray();

            Console.WriteLine($"Top {words.Length} words:");
            Console.WriteLine();

            for (int i = 0; i < words.Length; i++)
            {
                var word = words[i];
                Console.WriteLine($"  {i + 1, 2:N0}. {word.text} ({word.count:N0})");
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
        /// Imports the tweets from the specified Twitter archive directory into the configured
        /// <c>MongoDB</c> server, database and collection as an asynchronous operation.
        /// </summary>
        /// <param name="archivePath">The path to the directory containing the Twitter archive.</param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation to import the tweets into <c>MongoDb</c>.
        /// </returns>
        private static async Task ImportTweetsAsync(string archivePath)
        {
            Console.Write("Importing tweets into MongoDB...");

            string connectionString = ConfigurationManager.ConnectionStrings["MongoDB"].ConnectionString;
            string databaseName = ConfigurationManager.AppSettings["MongoDB:Database"];
            string collectionName = ConfigurationManager.AppSettings["MongoDB:Collection"];

            var client = new MongoClient();
            var database = client.GetDatabase(databaseName);

            var collection = database.GetCollection<BsonDocument>(collectionName);

            var tweets = ReadTweetsFromArchive(archivePath);

            int count = 0;
            var options = new UpdateOptions() { IsUpsert = true };

            foreach (var tweet in tweets)
            {
                var json = tweet.ToString();
                var document = BsonDocument.Parse(json);

                document["_id"] = document["id"];

                await collection.UpdateOneAsync(
                    (p) => p["_id"] == document["id"],
                    document,
                    options);

                count++;
            }

            Console.WriteLine($"imported/updated {count:N0} tweets.");
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
