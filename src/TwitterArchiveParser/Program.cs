// Copyright (c) Martin Costello, 2016. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.TwitterArchiveParser
{
    using System;
    using System.Reflection;
    using Newtonsoft.Json;

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
                // TODO
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
        /// Prints a banner to the console containing assembly and operating system information.
        /// </summary>
        private static void PrintBanner()
        {
            Assembly assembly = typeof(Program).Assembly;
            AssemblyName assemblyName = assembly.GetName();

            Console.WriteLine(
                $@"Twitter Archive Parser - v{assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion}
{assembly.GetCustomAttribute<AssemblyCopyrightAttribute>().Copyright}");
        }
    }
}
