using Azure.Storage.Files.Shares;
using Azure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Files.Shares.Specialized;
using System.Text.RegularExpressions;
using Azure.Storage.Files.Shares.Models;

namespace KenAzureFileShareLibrary
{
    internal static class InternalUtilities
    {
        /// <summary>
        /// Create a ShareFileClient using the supplied stream content.  
        /// Parms determine if an existing ShareFileClient can be overwritten and if parent directories should be created (if they don't already exist).
        /// </summary>
        /// <param name="shareFileClient"></param>
        /// <param name="streamContent"></param>
        /// <param name="allowOverwrite"></param>
        /// <param name="createCloudDirectories"></param>
        /// <exception cref="Exception"></exception>
        internal static void UploadFile(ShareFileClient shareFileClient, Stream streamContent, bool allowOverwrite, bool createCloudDirectories)
        {
            if (shareFileClient.Exists() && !allowOverwrite)
            {
                throw new Exception($"Not allowed to overwrite existing cloud file: {shareFileClient.Name}");
            }
            ShareDirectoryClient parentDir = shareFileClient.GetParentShareDirectoryClient();

            // Tricky! Create all parent directories!
            if (createCloudDirectories)
            {
                CreateParentDirectories(parentDir);
            }
            
            // create or replace file in cloud
            shareFileClient.Create(streamContent.Length);
            //  Azure allows for 4MB max uploads  (4 x 1024 x 1024 = 4194304)
            const int uploadLimit = 4000000; // Use round number instead of 4194304;
            if (streamContent.Length < uploadLimit)
            {
                streamContent.Seek(0, SeekOrigin.Begin);
                shareFileClient.UploadRange(new HttpRange(0, streamContent.Length), streamContent);
            }
            else
            {
                // Stream is larger than the limit so we need to upload in chunks
                streamContent.Seek(0, SeekOrigin.Begin);   // ensure stream is at the beginning
                int bytesRead;
                long index = 0;
                byte[] buffer = new byte[uploadLimit];
                while ((bytesRead = streamContent.Read(buffer, 0, buffer.Length)) > 0)
                {
                    // Create a memory stream for the buffer to upload
                    using (MemoryStream ms = new MemoryStream(buffer, 0, bytesRead))
                    {
                        shareFileClient.UploadRange(new HttpRange(index, ms.Length), ms);
                        index += ms.Length; // increment the index to the account for bytes already written
                    }
                }
            }
        }
        /// <summary>
        /// Copy contents of a local directory to a specific cloud directory (ShareDirectoryClient).
        /// A parm indicates if the method should recursively traverseAllSubdirectories.
        /// </summary>
        /// <param name="localPath"></param>
        /// <param name="cloudDirectory"></param>
        /// <param name="traverseAllSubdirectories"></param>
        internal static void UploadDirectoryContentToCloud(string localPath, ShareDirectoryClient cloudDirectory, bool traverseAllSubdirectories)
        {
            DirectoryInfo localDirInfo = new DirectoryInfo(localPath);
            // Cache directories before we start copying
            DirectoryInfo[] localSubdirInfos = localDirInfo.GetDirectories();
            //
            cloudDirectory.CreateIfNotExists();
            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in localDirInfo.GetFiles())
            {
                string localFilePath = file.FullName;
                ShareFileClient cloudFile = cloudDirectory.GetFileClient(file.Name);
                KenAzureFileUtilities.UploadFile(cloudFile, localFilePath, allowOverwrite: true);
            }
            // If recursive and copying subdirectories, recursively call this method
            if (traverseAllSubdirectories)
            {
                foreach (DirectoryInfo subDir in localSubdirInfos)
                {
                    ShareDirectoryClient cloudSubdir = cloudDirectory.GetSubdirectoryClient(subDir.Name);
                    UploadDirectoryContentToCloud(subDir.FullName, cloudSubdir, true);
                }
            }
        }
        internal static void DownloadCloudDirectoryContentToLocalDirectory(ShareDirectoryClient cloudDirectory, string localDirectoryPath, bool traverseAllSubdirectories)
        {
            Directory.CreateDirectory(localDirectoryPath);
            List<ShareFileClient> listCloudFiles = KenAzureFileUtilities.GetListAllCloudFiles(cloudDirectory, traverseAllSubdirectories);
            foreach (ShareFileClient cloudFile in listCloudFiles)
            {
                // Tricky! This will include the file name in addition to extra path segments!
                string extraPathSegments = cloudFile.Path.Substring(cloudDirectory.Path.Length+1);
                string localFilePath = Path.Combine(localDirectoryPath, extraPathSegments);
                // not really necessary; .NET will accept either separator (even intermixed!)
                localFilePath = localFilePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                KenAzureFileUtilities.DownloadFile(cloudFile, localFilePath, createLocalParentDirectories: true, allowOverwrite: true);
            }
        }
        /// <summary>
        /// Get list of all subdirectories in a cloud directory; not recursive.
        /// </summary>
        /// <param name="shareDirectoryClient"></param>
        /// <returns></returns>
        internal static List<ShareDirectoryClient> GetListAllCloudSubdirectoriesInCloudDirectory(ShareDirectoryClient cloudDirectory)
        {
            List<ShareDirectoryClient> listCloudSubdirectories = new List<ShareDirectoryClient>();

            // Note: The "name" and "IsDirectory" properties of a ShareFileItem can be used to obtain the ShareDirectoryClient or ShareFileClient for the item! 
            List<ShareFileItem> listShareFileItems = cloudDirectory.GetFilesAndDirectories().ToList();
            foreach (ShareFileItem shareItem in listShareFileItems)
            {
                if (shareItem.IsDirectory)
                {
                    ShareDirectoryClient subdir = cloudDirectory.GetSubdirectoryClient(shareItem.Name);
                    listCloudSubdirectories.Add(subdir);
                }
            }
            return listCloudSubdirectories;
        }
        /// <summary>
        /// Gets the size of all the files in a directory (and all subdirectories based on parm, traverseAllSubdirectories).
        /// </summary>
        /// <param name="localDirectoryPath"></param>
        /// <param name="traverseAllSubdirectories"></param>
        /// <returns></returns>
        internal static long GetLocalDirectorySize(string localDirectoryPath, bool traverseAllSubdirectories)
        {
            // Get array of all file names.
            SearchOption searchOption = traverseAllSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            string[] files = Directory.GetFiles(localDirectoryPath, "*.*", searchOption);
            // Calculate total bytes of all files in a loop.
            long totalBytes = 0;
            foreach (string name in files)
            {
                // Use FileInfo to get length of each file.
                FileInfo info = new FileInfo(name);
                totalBytes += info.Length;
            }
            // Return total size
            return totalBytes;
        }
        /// <summary>
        /// Gets the size of all the cloud files in a cloud directory (and all subdirectories based on parm, traverseAllSubdirectories).
        /// </summary>
        /// <param name="cloudDirectoryPath"></param>
        /// <param name="traverseAllSubdirectories"></param>
        /// <returns></returns>
        internal static long GetCloudDirectorySize(ShareDirectoryClient cloudDirectory, bool traverseAllSubdirectories)
        {
            // Get array of all file names.
            List<ShareFileClient> files = KenAzureFileUtilities.GetListAllCloudFiles(cloudDirectory, traverseAllSubdirectories);
            // Calculate total bytes of all files in a loop.
            long totalBytes = 0;
            foreach (ShareFileClient cloudFile in files)
            {
                totalBytes += ((ShareFileProperties)cloudFile.GetProperties()).ContentLength;
            }
            // Return total size
            return totalBytes;
        }
        /// <summary>
        /// Recursive method to create all parent directories plus the target directory!
        /// </summary>
        /// <param name="targetDir"></param>
        internal static void CreateParentDirectories(ShareDirectoryClient targetDir)
        {
            if (targetDir.Exists()) return;
            ShareDirectoryClient parentDir = targetDir.GetParentDirectoryClient();
            if (!parentDir.Exists())
            {
                CreateParentDirectories(parentDir);
                CreateParentDirectories(targetDir);
            }
            else
            {
                targetDir.Create();
            }
        }
        
        /// <summary>
        /// Ensure the object is a ShareFileClient or a ShareDirectoryClient and that the cloud file/directory physically exists!
        /// Throw an exception if the requirements are not satisfied.
        /// </summary>
        /// <param name="obj"></param>
        /// <exception cref="Exception"></exception>
        internal static void ThrowExceptionIfObjectNotExistingCloudFileOrCloudDirectory(object obj)
        {
            if ((obj.GetType() != typeof(ShareFileClient)) && (obj.GetType() != typeof(ShareFileClient)))
            {
                throw new Exception("Object is neither a ShareFileClient nor a ShareDirectoryClient");
            }
            if (obj.GetType() == typeof(ShareFileClient))
            {
                ShareFileClient shareFileClient = (ShareFileClient)obj;
                if (!shareFileClient.Exists())
                {
                    throw new Exception($"ShareFileClient does not exist: {shareFileClient.Uri.AbsolutePath}");
                }
            }
            if (obj.GetType() == typeof(ShareDirectoryClient))
            {
                ShareDirectoryClient shareDirectoryClient = (ShareDirectoryClient)obj;
                if (!shareDirectoryClient.Exists())
                {
                    throw new Exception($"ShareDirectoryClient does not exist: {shareDirectoryClient.Uri.AbsolutePath}");
                }
            }
        }
    }
}
