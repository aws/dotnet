using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;


namespace S3Demo
{
    class Program
    {
        const string BUCKET_NAME = "";

        const string TEST_FILE = "Halloween.jpg";
        static readonly int ONE_MEG = (int)Math.Pow(2, 20);

        static void Main(string[] args)
        {
            try
            {
                var client = new AmazonS3Client();

                PutObjectResponse putResponse = client.PutObject(new PutObjectRequest
                {
                    BucketName = BUCKET_NAME,
                    FilePath = TEST_FILE
                });

                GetObjectResponse getResponse = client.GetObject(new GetObjectRequest
                {
                    BucketName = BUCKET_NAME,
                    Key = TEST_FILE
                });

                getResponse.WriteResponseStreamToFile(@"c:\talk\" + TEST_FILE);


                var url = client.GetPreSignedURL(new GetPreSignedUrlRequest
                {
                    BucketName = BUCKET_NAME,
                    Key = TEST_FILE,
                    Expires = DateTime.Now.AddHours(1)
                });

                OpenURL(url);
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }


        #region Multi Part Upload Code
        /// <summary>
        /// Sample code to contrast uploading a file using Amazon S3's Multi-Part Upload API
        /// </summary>
        /// <param name="s3Client"></param>
        /// <param name="bucketName"></param>
        /// <param name="fileName"></param>
        static void UploadUsingMultiPartAPI(IAmazonS3 s3Client, string bucketName, string fileName)
        {
            const string objectKey = "multipart/myobject";

            // tell S3 we're going to upload an object in multiple parts and receive an upload ID
            // in return
            var initializeUploadRequest = new InitiateMultipartUploadRequest
            {
                BucketName = bucketName,
                Key = objectKey
            };
            var initializeUploadResponse = s3Client.InitiateMultipartUpload(initializeUploadRequest);

            // this ID must accompany all parts and the final 'completed' call
            var uploadID = initializeUploadResponse.UploadId;

            // Send the file (synchronously) using 4*5MB parts - note we pass the upload id
            // with each call. For each part we need to log the returned etag value to pass
            // to the completion call
            var partETags = new List<PartETag>();
            var partSize = 5 * ONE_MEG; // this is the minimum part size allowed

            for (var partNumber = 0; partNumber < 4; partNumber++)
            {
                // part numbers must be between 1 and 1000
                var logicalPartNumber = partNumber + 1;
                var uploadPartRequest = new UploadPartRequest
                {
                    BucketName = bucketName,
                    Key = objectKey,
                    UploadId = uploadID,
                    PartNumber = logicalPartNumber,
                    PartSize = partSize,
                    FilePosition = partNumber * partSize,
                    FilePath = fileName
                };

                var partUploadResponse = s3Client.UploadPart(uploadPartRequest);
                partETags.Add(new PartETag { PartNumber = logicalPartNumber, ETag = partUploadResponse.ETag });
            }

            var completeUploadRequest = new CompleteMultipartUploadRequest
            {
                BucketName = bucketName,
                Key = objectKey,
                UploadId = uploadID,
                PartETags = partETags
            };

            s3Client.CompleteMultipartUpload(completeUploadRequest);
        }

        #endregion


        /// <summary>
        /// Utility method for opening a URL in a browser
        /// </summary>
        /// <param name="url"></param>
        private static void OpenURL(string url)
        {
            var psi = new ProcessStartInfo("iexplore.exe");
            psi.Arguments = url;
            Process.Start(psi);
        }
    }
}
