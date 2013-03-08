using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Amazon.S3;
using Amazon.S3.Model;
using System.Configuration;

namespace ops
{
    class Install
    {
        string myArg = String.Empty;
        public string Name { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public string PackageLocation { get; set; }
        public string InstallationPath { get; set; }
        public string Run { get; set; }
        public string Arg
        {
            get
            {
                return this.myArg.Replace("[TEMPDIR]", ConfigurationManager.AppSettings["TempDir"]);
            }
            set { myArg = value; }
        }
            
        public bool Active { get; set; }
        public int InstallOrder { get; set; }
    }

    class S3Download
    {
        public string S3AccessKey { get; set; }
        public string S3AccessPass { get; set; }
        public string S3Bucket { get; set; }
        public string S3RemotePath { get; set; }
        public string S3Localpath { get; set; }
        public string Status { get; set; }
        public bool HasError { get; set; }
        public string ErrorMsg { get; set; }




        public void StartDownload()
        {
            string bucketName = this.S3Bucket; 
            string key = this.S3RemotePath;
            string dest = this.S3Localpath;

            Console.WriteLine("Download " + key + " and saving it in " + dest);

            using (AmazonS3 client = Amazon.AWSClientFactory.CreateAmazonS3Client(this.S3AccessKey, this.S3AccessPass))
            {
                GetObjectRequest getObjectRequest = new GetObjectRequest().WithBucketName(bucketName).WithKey(key);

                using (S3Response getObjectResponse = client.GetObject(getObjectRequest))
                {
                   
                        using (Stream s = getObjectResponse.ResponseStream)
                        {
                            using (FileStream fs = new FileStream(dest, FileMode.Create, FileAccess.Write))
                            {
                                byte[] data = new byte[32768];
                                int bytesRead = 0;
                                do
                                {
                                    bytesRead = s.Read(data, 0, data.Length);
                                    fs.Write(data, 0, bytesRead);
                                }
                                while (bytesRead > 0);
                                fs.Flush();
                            }
                        }
                    
                }
            }

        }

    }
}
