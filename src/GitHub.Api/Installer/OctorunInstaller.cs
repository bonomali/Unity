﻿using System;
using System.Threading;
using GitHub.Logging;

namespace GitHub.Unity
{
    class OctorunInstaller
    {
        private static readonly ILogging Logger = LogHelper.GetLogger<OctorunInstaller>();

        private readonly IEnvironment environment;
        private readonly IFileSystem fileSystem;
        private readonly ITaskManager taskManager;
        private readonly IZipHelper sharpZipLibHelper;
        private readonly OctorunInstallDetails installDetails;

        public OctorunInstaller(IEnvironment environment, ITaskManager taskManager,
            OctorunInstallDetails installDetails = null)
        {
            this.environment = environment;
            this.sharpZipLibHelper = ZipHelper.Instance;
            this.installDetails = installDetails ?? new OctorunInstallDetails(environment.UserCachePath);
            this.fileSystem = environment.FileSystem;
            this.taskManager = taskManager;
        }

        public NPath SetupOctorunIfNeeded()
        {
            //Logger.Trace("SetupOctorunIfNeeded");

            NPath path = NPath.Default;
            var isOctorunExtracted = IsOctorunExtracted();
            Logger.Trace("isOctorunExtracted: {0}", isOctorunExtracted);
            if (isOctorunExtracted)
                path = installDetails.ExecutablePath;
            GrabZipFromResources();

            if (!path.IsInitialized)
            {
                var tempZipExtractPath = NPath.CreateTempDirectory("octorun_extract_archive_path");
                var unzipTask = new UnzipTask(taskManager.Token, installDetails.ZipFile,
                        tempZipExtractPath, sharpZipLibHelper,
                        fileSystem);
                var extractPath = unzipTask.RunWithReturn(true);
                if (unzipTask.Successful)
                    path = MoveOctorun(extractPath.Combine("octorun"));
                tempZipExtractPath.DeleteIfExists();
            }
            return path;
        }

        private NPath GrabZipFromResources()
        {
            installDetails.ZipFile.DeleteIfExists();
            
            AssemblyResources.ToFile(ResourceType.Generic, "octorun.zip", installDetails.BaseZipPath, environment);
            
            return installDetails.ZipFile;
        }

        private NPath MoveOctorun(NPath fromPath)
        {
            var toPath = installDetails.InstallationPath;
            Logger.Trace($"Moving tempDirectory:'{fromPath}' to extractTarget:'{toPath}'");

            toPath.DeleteIfExists();
            toPath.EnsureParentDirectoryExists();
            fromPath.Move(toPath);
            return installDetails.ExecutablePath;
        }

        private bool IsOctorunExtracted()
        {
            if (!installDetails.InstallationPath.DirectoryExists())
            {
                //Logger.Warning($"{octorunPath} does not exist");
                return false;
            }

            if (!installDetails.VersionFile.FileExists())
            {
                //Logger.Warning($"{versionFilePath} does not exist");
                return false;
            }

            var octorunVersion = installDetails.VersionFile.ReadAllText();
            if (!OctorunInstallDetails.PackageVersion.Equals(octorunVersion))
            {
                Logger.Warning("Current version {0} does not match expected {1}", octorunVersion, OctorunInstallDetails.PackageVersion);
                return false;
            }
            return true;
        }

        public class OctorunInstallDetails
        {
            public const string DefaultZipMd5Url = "https://ghfvs-installer.github.com/unity/octorun/octorun.zip.md5";
            public const string DefaultZipUrl = "https://ghfvs-installer.github.com/unity/octorun/octorun.zip";

            public const string PackageVersion = "9fcd9faa";
            private const string PackageName = "octorun";
            private const string zipFile = "octorun.zip";

            public OctorunInstallDetails(NPath baseDataPath)
            {
                BaseZipPath = baseDataPath.Combine("downloads");
                BaseZipPath.EnsureDirectoryExists();
                ZipFile = BaseZipPath.Combine(zipFile);

                var installPath = baseDataPath.Combine(PackageName);
                InstallationPath = installPath;

                Executable = "app.js";
                ExecutablePath = installPath.Combine("src", "bin", Executable);
            }

            public NPath BaseZipPath { get; }
            public NPath ZipFile { get; }
            public NPath InstallationPath { get; }
            public string Executable { get; }
            public NPath ExecutablePath { get; }
            public UriString ZipMd5Url { get; set; } = DefaultZipMd5Url;
            public UriString ZipUrl { get; set; } = DefaultZipUrl;
            public NPath VersionFile => InstallationPath.Combine("version");
        }
    }
}