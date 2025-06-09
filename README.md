# KenAzureFileShareLibrary
## Warranty Disclaimer:
The software is provided "AS IS," without any warranties, express or implied. 
IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, 
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR 
THE USE OR OTHER DEALINGS IN THE SOFTWARE.
## Purpose
Ken's library simplifies using some functionality in the The official Azure Storage SDK for .NET.   
These are the most useful methods in this library; examples are provided later in this document.
- GetShareClient - gives authorization which is used directly or indirectly in all other methods.
- UploadFile - from a local file, from a byte array
- DownloadFile - to a local file, to a byte array
- DownloadCloudDirectoryToLocalDirectory
- UploadDirectoryContentToCloud
- GetListAllCloudFiles (optionally traverseAllSubdirectories)
- GetListAllCloudDirectories (optionally traverseAllSubdirectories)
- GetMetadataDictionary - for a cloud file or cloud directory
- AddMetadataItem - for a cloud file or cloud directory
- RemoveMetadataItem - for a cloud file or cloud directory
- ClearMetadata - for a cloud file or cloud directory
- Methods in KenMapping class:
    - MapLocalFilePathToCloudFile
    - MapLocalDirectoryPathToCloudDirectory
    - MapCloudFileToLocalFilePath
    - MapCloudDirectoryToLocalDirectoryPath

## Terminology 
**ShareClient:**          The ShareClient allows you to manipulate Azure Storage shares and their directories and files.    
                          The constructor requires the Azure authorization keys.   (Ken also calls it FileShare)   
**ShareFileClient:**      represents a FILE in the cloud.      (Ken also calls it CloudFile)   
**ShareDirectoryClient:** represents a DIRECTORY in the cloud. (Ken also calls it CloudDirectory)

**Note 1:** When you "Create" a ShareFileClient or ShareFileDirectory that cloud object is not physically created!   
A physical file/directory gets created when you act on the object (ex: Upload will create the physical file).

**Note 2:** Perhaps Ken should not have used the term FileShare for a ShareClient.  
Note that ShareClient (a.k.a. FileShare) is NOT the same as a ShareFileClient (a.k.a. CloudFile).

## Setup
You must have an Azure account with a defined storage account.   
Your **azureStorageConnectionString** can be found on the Azure portal at:    
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;your_azure_storageaccount/Data storage/Security+networking/Access keys.    
Your **fileShareName** (after you create one) can be found on the Azure portal at:    
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;your_azure_storageaccount/Data storage/File shares.

## Examples (for Windows based application)
### GetShareClient
```
// Ken often refers to a ShareClient as a FileShare!
// This ShareClient (FileShare) will be obtained once and used for many methods in KenAzureUtilities.
ShareClient KenTutorialFileShare { get; set; }

string azureStorageConnectionString = "<your_azureStorageConnectionString>""; // See Setup above
string fileShareName = "kentutorialfileshare";
KenTutorialFileShare = KenAzureFileUtilities.GetShareClient(azureStorageConnectionString, fileShareName, createIfNotExist: true);
```
### UploadFile (from local file)
```
// This local file must exist!
string localFilename = "TestFile1.txt";
string localFolderPath = @"F:\KenTestAzure\testfolder1\testfolder2";
string localFilepath = Path.Combine(localFolderPath, localFilename);
// It does not matter if the cloudFilePath starts with a '/' (A leading '/' is essentially ignored).
// If cloudFilepath specifies only the localFilename, the file will be uploaded to the file share's root directory!
string cloudFilepath = $"KenTestAzure/testfolder1/testfolder2/{localFilename}";
ShareFileClient shareFileClient = KenAzureFileUtilities.CreateShareFileClient(KenTutorialFileShare, cloudFilepath);
KenAzureFileUtilities.UploadFile(shareFileClient, localFilepath, allowOverwrite: true, createCloudDirectories: true);
string message = "Successfully uploaded file using local file path.";
MessageBox.Show(message, "TestKensAzureLibrary", MessageBoxButtons.OK, MessageBoxIcon.Information);
```

### UploadFile (from byte array)
```
// These bytes are assigned trivially for testing!
// For real scenarios the byte array could be the bytes of an Image or many other things.
byte[] bytes = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 };
// specify the desired cloudFilepath for the bytes we are uploading
string filename = "TestFile2.bin";
string cloudFilepath = $"KenTestAzure/testfolder1/{filename}";
ShareFileClient shareFileClient = KenAzureFileUtilities.CreateShareFileClient(KenTutorialFileShare, cloudFilepath);
KenAzureFileUtilities.UploadFile(shareFileClient, bytes, allowOverwrite: true);
string message = "Successfully uploaded binary data using byte array.";
MessageBox.Show(message, "TestKensAzureLibrary", MessageBoxButtons.OK, MessageBoxIcon.Information);
```

### DownloadFile (to local file)
```
// You need an existing cloud file.  You could use UploadFile above or the Microsoft Azure Storage Explorer Tool.
string cloudFilepath = $"KenTestAzure/testfolder1/testfolder2/TestFile1.txt";
ShareFileClient shareFileClient = KenAzureFileUtilities.CreateShareFileClient(KenTutorialFileShare, cloudFilepath);
// Ken's library has a KenMapping class as a tool to map between local and cloud file names but we just hard code the local filepath here.
string localFilePath = @"F:\KenTestAzureDownload\testfolder1\testfolder2\TestFile1.txt";
KenAzureFileUtilities.DownloadFile(shareFileClient, localFilePath, createLocalParentDirectories: true, allowOverwrite: true);
string message = $"Successfully downloaded file to {localFilePath}.";
MessageBox.Show(message, "TestKensAzureLibrary", MessageBoxButtons.OK, MessageBoxIcon.Information);
```

### DownloadFile (to byte array)
```
// You need an existing cloud file.  You could use UploadFile above or the Microsoft Azure Storage Explorer Tool.
string cloudFilepath = $"KenTestAzure/testfolder1/TestFile2.bin";
ShareFileClient shareFileClient = KenAzureFileUtilities.CreateShareFileClient(KenTutorialFileShare, cloudFilepath);
byte[] bytes = KenAzureFileUtilities.DownloadFile(shareFileClient);
string message = $"Successfully downloaded file to byte array: {BitConverter.ToString(bytes)}.";
MessageBox.Show(message, "TestKensAzureLibrary", MessageBoxButtons.OK, MessageBoxIcon.Information);
```

### MapLocalFilePathToCloudFile
```
// Specify the top level cloud directory and the top level local directory for mapping.
KenMapping kenMapping = new KenMapping(KenTutorialFileShare, "/KenTestAzure", @"F:\KenTestAzure");
string localFilename = "TestFile1.txt";
string localFolderPath = @"F:\KenTestAzure\testfolder1";
string localFilepath = Path.Combine(localFolderPath, localFilename);
ShareFileClient cloudFile = kenMapping.MapLocalFilePathToCloudFile(localFilepath);
string message = $"Mapped cloud file: \n{cloudFile.Uri.AbsolutePath} \n to local file: \n{localFilepath}";
MessageBox.Show(message, "TestKensAzureLibrary", MessageBoxButtons.OK, MessageBoxIcon.Information);
```

### MapCloudFileToLocalFilePath
```
// Specify the top level cloud directory and the top level local directory for mapping.
KenMapping kenMapping = new KenMapping(KenTutorialFileShare, "/KenTestAzure", @"F:\KenTestAzure");
string cloudFilePath = $"KenTestAzure/testfolder1/TestFile2.bin";
ShareFileClient cloudFile = KenAzureFileUtilities.CreateShareFileClient(KenTutorialFileShare, cloudFilePath);
string localFilepath = kenMapping.MapCloudFileToLocalFilePath(cloudFile);
string message = $"Mapped cloud file: \n{cloudFile.Uri.AbsolutePath} \n to local file: \n{localFilepath}";
MessageBox.Show(message, "TestKensAzureLibrary", MessageBoxButtons.OK, MessageBoxIcon.Information);
```

### AddMetadataItem (example adds two metadata items)
```
string cloudFilePath = $"KenTestAzure/testfolder1/TestFile2.bin";
ShareFileClient cloudFile = KenAzureFileUtilities.CreateShareFileClient(KenTutorialFileShare, cloudFilePath);
// specify my metadata
string myMetadataKey = "Country";
string myMetadataValue = "USA data";
KenAzureFileUtilities.AddMetadataItem(cloudFile, myMetadataKey, myMetadataValue, replaceIfKeyAlreadyExists: true);
myMetadataKey = "Year";
myMetadataValue = "1953";
KenAzureFileUtilities.AddMetadataItem(cloudFile, myMetadataKey, myMetadataValue, replaceIfKeyAlreadyExists: true);
string message = $"Added metadata item to cloud file: {cloudFile.Uri.AbsolutePath}";
MessageBox.Show(message, "TestKensAzureLibrary", MessageBoxButtons.OK, MessageBoxIcon.Information);
```

### RemoveMetadataItem
```
string cloudFilePath = $"KenTestAzure/testfolder1/TestFile2.bin";
ShareFileClient cloudFile = KenAzureFileUtilities.CreateShareFileClient(KenTutorialFileShare, cloudFilePath);
// specify metadata item to remove
string myMetadataKey = "Country";
KenAzureFileUtilities.RemoveMetadataItem(cloudFile, myMetadataKey);
string message = $"Removed metadata item from cloud file: {cloudFile.Uri.AbsolutePath}";
MessageBox.Show(message, "TestKensAzureLibrary", MessageBoxButtons.OK, MessageBoxIcon.Information);
```

### ClearMetadata
```
string cloudFilePath = $"KenTestAzure/testfolder1/TestFile2.bin";
ShareFileClient cloudFile = KenAzureFileUtilities.CreateShareFileClient(KenTutorialFileShare, cloudFilePath);
KenAzureFileUtilities.ClearMetadata(cloudFile);
string message = $"Cleared all metadata from cloud file: {cloudFile.Uri.AbsolutePath}";
MessageBox.Show(message, "TestKensAzureLibrary", MessageBoxButtons.OK, MessageBoxIcon.Information);
```

### GetMetadataDictionary
```
string cloudFilePath = $"KenTestAzure/testfolder1/TestFile2.bin";
ShareFileClient cloudFile = KenAzureFileUtilities.CreateShareFileClient(KenTutorialFileShare, cloudFilePath);
ShareFileProperties shareFileProperties = cloudFile.GetProperties();
Dictionary<string, string> metadataDict = (Dictionary<string, string>)shareFileProperties.Metadata;
string message = $"Current metadata for cloud file: {cloudFile.Uri.AbsolutePath}";
foreach (KeyValuePair<string, string> kvp in metadataDict)
{
    message += $"\n{kvp.Key}={kvp.Value}";
}
MessageBox.Show(message, "TestKensAzureLibrary", MessageBoxButtons.OK, MessageBoxIcon.Information);
```


