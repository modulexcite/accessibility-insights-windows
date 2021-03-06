﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace AccessibilityInsights.SetupLibrary
{
    /// <summary>
    /// Methods to provide support when switching versions (upgrading or changing channels)
    /// </summary>
    public static class VersionSwitcherWrapper
    {
        /// <summary>
        /// Installs a more recent version in response to an upgrade (retain the same channel)
        /// </summary>
        /// <param name="installerUri">The uri to the web-hosted installer</param>
        public static void InstallUpgrade(Uri installerUri)
        {
            DownloadAndInstall(installerUri, null);
        }

        /// <summary>
        /// Installs a different version in response to a channel change
        /// </summary>
        /// <param name="newChannel">The new channel to use</param>
        public static void ChangeChannel(ReleaseChannel newChannel)
        {
            if (ChannelInfoUtilities.TryGetChannelInfo(newChannel, out ChannelInfo channelInfo, null)
                && channelInfo.IsValid)
            {
                DownloadAndInstall(new Uri(channelInfo.InstallAsset), newChannel);
                return;
            }

            throw new ArgumentException("Unable to get channel information", nameof(newChannel));
        }

        /// <summary>
        /// Private method that does the work shared between InstallUpgrade and ChangeChannel
        /// </summary>
        /// <param name="installerUrl">The uri to the web-hosted installer</param>
        /// <param name="newChannel">If not null, the new channel to select</param>
        private static void DownloadAndInstall(Uri installerUri, ReleaseChannel? newChannel)
        {
            List<FileStream> fileLocks = new List<FileStream>();
            try
            {
                string installedFolder = GetInstalledVersionSwitcherFolder();
                string temporaryFolder = GetTemporaryVersionSwitcherFolder();
                RemoveFolder(temporaryFolder);
                RecursiveTreeCopy(installedFolder, temporaryFolder, fileLocks);
                ProcessStartInfo start = new ProcessStartInfo
                {
                    FileName = Path.Combine(temporaryFolder, "AccessibilityInsights.VersionSwitcher.exe"),
                    Arguments = GetVersionSwitcherArguments(installerUri, newChannel)
                };
                Process.Start(start);
            }
            finally
            {
                // Release all of our file locks
                foreach (FileStream fileLock in fileLocks)
                {
                    fileLock.Dispose();
                }
            }
        }

        /// <summary>
        /// Remove a previous folder, if it exists
        /// </summary>
        private static void RemoveFolder(string folderToDelete)
        {
            if (Directory.Exists(folderToDelete))
            {
                Directory.Delete(folderToDelete, true);
            }
        }

        /// <summary>
        /// TreeCopy source to dest, keeping a lock on each copied file to prevent tampering
        /// </summary>
        private static void TreeCopy(string source, string dest, List<FileStream> fileLocks)
        {
            if (!Directory.Exists(source))
                throw new ArgumentException("No Source folder found", nameof(source));

            RecursiveTreeCopy(source, dest, fileLocks);
        }

        /// <summary>
        /// Core function for TreeCopy. Keeps a lock on each file that gets copied to prevent tampering
        /// </summary>
        private static void RecursiveTreeCopy(string source, string dest, List<FileStream> fileLocks)
        {
            if (!Directory.Exists(dest))
            {
                Directory.CreateDirectory(dest);
            }

            // copy folders
            foreach (string dir in Directory.GetDirectories(source))
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(dir);
                RecursiveTreeCopy(dir, Path.Combine(dest, directoryInfo.Name), fileLocks);
            }

            // copy files, keeping a FileStream to each (to prevent someone from changing them on us)
            foreach (string file in Directory.GetFiles(source))
            {
                FileInfo fileInfo = new FileInfo(file);
                string destFile = Path.Combine(dest, fileInfo.Name);
                fileInfo.CopyTo(destFile, true);
                fileLocks.Add(File.OpenRead(destFile));
            }
        }

        /// <summary>
        /// Extract the path to the installed version switcher, based on the app path. The
        /// VersionSwitcher is a sibling folder of the installed app folder
        /// </summary>
        private static string GetInstalledVersionSwitcherFolder()
        {
            string appPath = MsiUtilities.GetAppInstalledPath();
            string root = Path.GetDirectoryName(appPath);
            string versionSwitcherFolder = Path.Combine(root, "VersionSwitcher");
            return versionSwitcherFolder;
        }

        /// <summary>
        /// Find the folder where the temporary VersionSwitcher will go
        /// </summary>
        private static string GetTemporaryVersionSwitcherFolder()
        {
            string tempPath = Path.GetTempPath();
            return Path.Combine(tempPath, "VersionSwitcher");
        }

        /// <summary>
        /// Create the arguments to pass to the Version Switcher process
        /// </summary>
        /// <param name="installerUri">The uri to the web-hosted installer</param>
        /// <param name="newChannel">If not null, the new channel to select</param>
        private static string GetVersionSwitcherArguments(Uri installerUri, ReleaseChannel? newChannel)
        {
            string arguments = installerUri.ToString();

            if (newChannel.HasValue)
            {
                arguments += " " + newChannel.Value.ToString();
            }

            return arguments;
        }
    }
}
