// Copyright (c) Martin Costello, 2016. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace MartinCostello.TwitterArchiveParser
{
    /// <summary>
    /// A class containing JSON extension methods. This class cannot be inherited.
    /// </summary>
    internal static class JsonExtensions
    {
        /// <summary>
        /// Gets the date the tweet was posted from the specified element.
        /// </summary>
        /// <param name="element">The JSON document to get the tweet's posting date from.</param>
        /// <returns>
        /// The <see cref="DateTimeOffset"/> the tweet was posted.
        /// </returns>
        internal static DateTimeOffset GetCreatedDate(this JsonElement element)
        {
            string createdAt = element.GetProperty("created_at").GetString();

            return DateTimeOffset.ParseExact(
                createdAt,
                "ddd MMM dd HH:mm:ss zzz yyyy",
                CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Gets the tweet from the specified element.
        /// </summary>
        /// <param name="element">The JSON document to get the tweet text from.</param>
        /// <returns>
        /// A <see cref="string"/> containing the tweet's text.
        /// </returns>
        internal static string GetTweet(this JsonElement element)
            => element.GetProperty("full_text").GetString();

        /// <summary>
        /// Gets the tweets from the specified document.
        /// </summary>
        /// <param name="document">The JSON document to get the tweets from.</param>
        /// <returns>
        /// An <see cref="IEnumerable{T}"/> that returns the tweets in the specified document.
        /// </returns>
        internal static IEnumerable<JsonElement> GetTweets(this JsonDocument document)
            => document.RootElement.EnumerateArray().Select((p) => p.GetProperty("tweet"));
    }
}
