using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Diagnostics;
using System.Windows.Forms;
using System.Configuration;
using System.Threading;
using System.Management;

namespace ops
{
    class Program
    {

        static string argProc(string [] args, string key)
        {
            string ret = null;
            foreach (string arg in args)
            {
                if (arg.Contains("="))
                {
                    string[] arr = arg.Split('=');
                    if (arr[0].ToLower().Trim() == key.ToLower().Trim())
                    {
                        ret = arr[1];
                    }
                }
            }

            return ret;
        }
        
        static void Main(string[] args)
        {
           
            
            if (!String.IsNullOrEmpty(argProc(args,"-help")) ||
                !String.IsNullOrEmpty(argProc(args, "-h")) ||
                !String.IsNullOrEmpty(argProc(args, "--help")) ||
                !String.IsNullOrEmpty(argProc(args,"--h")) ||
                !String.IsNullOrEmpty(argProc(args,"/help")) ||
                !String.IsNullOrEmpty(argProc(args,"/h")))
            {
                ShowHelp();
                return;
            }
            List<Install> installables = new List<Install>();

            switch (args[0].ToLower().Replace("-",""))
            {
                case "install":
                    string config = args[1];
                    if (!File.Exists(config.Trim()))
                    {
                        Console.WriteLine("You must include a valid opsconfig.xml file." + Environment.NewLine);
                        ShowHelp();
                        return;
                    }
                    XmlDocument xdoc;
                    try
                    {
                         xdoc = new XmlDocument();
                        xdoc.LoadXml(File.ReadAllText(args[1].Trim()));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("ERROR - XML Config is not found or malformed. " + ex.ToString());
                        return;
                    }
                    XmlNodeList installnodes = xdoc.SelectNodes("//Installations/Install");
                    XmlNodeList manualSteps = xdoc.SelectNodes("//ManualInstallationSteps/step");
                    String Domain = xdoc.SelectSingleNode("//DomainConfig/DomainName").InnerText;
                    String MachineName = xdoc.SelectSingleNode("//DomainConfig/MachineName").InnerText;
                    String AdminUserName = xdoc.SelectSingleNode("//DomainConfig/AdminUserName").InnerText;
                    String AdminPassword = xdoc.SelectSingleNode("//DomainConfig/AdminPassword").InnerText;


                    foreach (XmlNode item in installnodes)
                    {
                        Install install = new Install();
                        install.Name = item["name"].InnerText;
                        install.Run = item["run"].InnerText;
                        install.InstallationPath = item["installationPath"].InnerText;
                        install.PackageLocation = item["pkgLocation"].InnerText;
                        install.InstallOrder = Convert.ToInt32(item["installOrder"].InnerText);
                        install.Active = Convert.ToBoolean(item["active"].InnerText);
                        install.Version = item["version"].InnerText;
                        install.Description = item["desc"].InnerText;
                        install.Arg = item["arguments"].InnerText;

                        installables.Add(install);
                        
                        
                    }
                    bool flag = true;
                    var orders = (from o in installables where o.Active == flag orderby o.InstallOrder select o);
                    Console.WriteLine(String.Format("Order\tName\tVersion\tDescription") + Environment.NewLine);
                    foreach (var line in orders){
                        Console.WriteLine(String.Format("{0}\t{1}\t{2}\t{3}", line.InstallOrder, line.Name, line.Version, line.Description) + Environment.NewLine);
                    }
                    Console.WriteLine("Ready to install?[y/n]");

                    string ans = Console.ReadLine();
                    if (ans.ToLower() != "y" && ans.ToLower() != "yes")
                        return;

                    Console.WriteLine("Do you want to join this box to the domain?[y/n]");
                    string dans = Console.ReadLine();

                    if (dans.ToLower() == "y")
                    {
                        
                        Console.WriteLine(" --------  Joining the domain  ---------");
                        if (String.IsNullOrEmpty(MachineName))
                        {
                            Console.WriteLine("Name for the current machine? ");
                            MachineName = Console.ReadLine();
                        }

                        if (String.IsNullOrEmpty(Domain))
                        {
                            Console.WriteLine("Domain to join? ");
                            Domain = Console.ReadLine();
                        }
                        if (String.IsNullOrEmpty(AdminUserName))
                        {
                            Console.WriteLine("Admin Name: ");
                            AdminUserName = Console.ReadLine();
                        }
                        if (String.IsNullOrEmpty(AdminPassword))
                        {
                            Console.WriteLine("Admin Password: ");
                            AdminPassword = Console.ReadLine();
                        }

                        if (string.IsNullOrEmpty(MachineName) ||
                            string.IsNullOrEmpty(Domain) ||
                            string.IsNullOrEmpty(AdminUserName) ||
                            string.IsNullOrEmpty(AdminPassword))
                        {
                            Console.WriteLine("Missing information - please try again.");
                            return;
                        }

                        if (!JoinAndSetName(Domain, MachineName, AdminUserName, AdminPassword))
                        {
                            Console.WriteLine("Joining the Domain failed!  Install process now exists.");
                            return;
                        }
                        else
                        {
                            Console.WriteLine("SUCCESS!  " + MachineName+ " was joined to " + Domain);
                        }
                    }

                    String nodownload = argProc(args, "nodownload");
                    if (!string.IsNullOrEmpty(nodownload))
                    {
                       Console.WriteLine("-----  Downloading skipped  --------------");
                    }
                    else
                    {
                        DownloadFromS3(orders);
                    }
                    
            

                    //start execution of processes
                    foreach (var exe in orders)
                    {
                        if (String.IsNullOrEmpty(exe.Run))
                            continue;

                        Console.WriteLine("=============== Now doing " + exe.Name);
                       
                        
            
                        System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                        startInfo.FileName = exe.Run;
                        startInfo.UseShellExecute = false;
                        startInfo.Arguments = exe.Arg;
                        startInfo.RedirectStandardOutput = true;
            
                        try
                        {
                            using (Process process = Process.Start(startInfo))
                            {
                                //
                                // Read in all the text from the process with the StreamReader.
                                //
                                process.WaitForExit();
                                using (StreamReader reader = process.StandardOutput)
                                {
                                    string result = reader.ReadToEnd();
                                    Console.Write(result);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(Environment.NewLine +   "ERROR - Something has gone wrong while installing.  Copy/Paste and report to amin please " + exe.Name + "  " + startInfo.FileName + "  " + startInfo.Arguments + "  " +  Environment.NewLine + ex.ToString() + Environment.NewLine);
                            Console.WriteLine("Continue? [y/n]");
                            string cont = Console.ReadLine();
                            if (cont.ToLower() == "n")
                            {
                          
                                return;
                            }

                        }
                        
                        Console.WriteLine("Done installing " + exe.Name);
                  }

                    //Manual steps

                    Console.WriteLine("===========  Manal Steps  ===============");

                    foreach (XmlNode step in manualSteps)
                    {
                        Console.WriteLine("=================================");
                        Console.WriteLine(step["Number"].InnerText + ": " + step["Title"].InnerText);
                        Console.WriteLine(step["Instructions"].InnerText);
                        Console.WriteLine("Continue? [y/n]");
                        string move = Console.ReadLine();
                        if (move.ToLower() == "n")
                            return;
                        else
                            continue;

                    }

                  
                    break;
            }





        }

        public static bool JoinAndSetName(string domainName, string newName, string AdminName, string AdminPass)
        {
            Console.WriteLine( string.Format("Joining domain and changing Machine Name from '{0}' to '{1}'...", Environment.MachineName, newName));

            // Get WMI object for this machine
            using (ManagementObject wmiObject = new ManagementObject(new ManagementPath("Win32_ComputerSystem.Name='" + Environment.MachineName + "'")))
            {
                try
                {
                    // Obtain in-parameters for the method
                    ManagementBaseObject inParams = wmiObject.GetMethodParameters("JoinDomainOrWorkgroup");
                    inParams["Name"] = domainName;
                    inParams["Password"] = AdminPass;
                    inParams["UserName"] = AdminName;
                    inParams["FJoinOptions"] = 3; // Magic number: 3 = join to domain and create computer account

                    Console.WriteLine( string.Format("Joining machine to domain under name '{0}'...", inParams["Name"]));

                    // Execute the method and obtain the return values.
                    ManagementBaseObject joinParams = wmiObject.InvokeMethod("JoinDomainOrWorkgroup", inParams, null);

                    Console.WriteLine( string.Format("JoinDomainOrWorkgroup return code: '{0}'", joinParams["ReturnValue"]));

                    // Did it work?
                    if ((uint)(joinParams.Properties["ReturnValue"].Value) != 0)
                    {
                        // Join to domain didn't work
                        Console.WriteLine(string.Format("JoinDomainOrWorkgroup failed with return code: '{0}'", joinParams["ReturnValue"]));
                        return false;
                    }
                }
                catch (ManagementException e)
                {
                    // Join to domain didn't work
                    Console.WriteLine( string.Format("Unable to join domain '{0}'"), e);
                    return false;
                }

                // Join to domain worked - now change name
                ManagementBaseObject inputArgs = wmiObject.GetMethodParameters("Rename");
                inputArgs["Name"] = newName;
                inputArgs["Password"] = AdminPass;
                inputArgs["UserName"] = AdminName;

                // Set the name
                ManagementBaseObject nameParams = wmiObject.InvokeMethod("Rename", inputArgs, null);
                Console.WriteLine( string.Format("Machine Rename return code: '{0}'", nameParams["ReturnValue"]));

                if ((uint)(nameParams.Properties["ReturnValue"].Value) != 0)
                {
                    // Name change didn't work
                    Console.WriteLine( string.Format("Unable to change Machine Name from '{0}' to '{1}'", Environment.MachineName, newName));
                    return false;
                }

                // All ok
                return true;
            }
        }

        private static void DownloadFromS3(IOrderedEnumerable<Install> orders)
        {



            
            foreach (var item in orders)
            {
                if (String.IsNullOrEmpty(item.PackageLocation))
                    continue;
                Console.WriteLine("Downloading " + item.Name + " from S3");
                try
                {
                    S3Download sd = new S3Download();
                    sd.S3AccessKey = ConfigurationManager.AppSettings["S3AccessKey"].ToString();
                    sd.S3AccessPass = ConfigurationManager.AppSettings["S3AccessPass"].ToString();

                    
                    FileInfo fi = new FileInfo(item.PackageLocation);

                    sd.S3Bucket = ConfigurationManager.AppSettings["S3bucket"];


                        sd.S3Localpath = fi.Name;

                    
                    sd.S3RemotePath = item.PackageLocation;

                    sd.Status = "Start";
                    sd.HasError = false;
                    sd.ErrorMsg = "";

                    sd.StartDownload();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR- Downloading " + item.Name + "(" + item.PackageLocation + " " + item.Run + ") threw an error. " +  ex.ToString());
                    Console.WriteLine("Continue downloading? [y/n]");
                    string ans = Console.ReadLine();
                    if (ans.ToLower().StartsWith("n"))
                        return;
                    else
                        continue;

                }
            }
           
        }

        private static void DownloadFromS3(Install exe)
        {
           

            return;
        }

        private static void ShowHelp()
        {
           // throw new NotImplementedException();
            Console.WriteLine("Usage: ops.exe install opsconfig.xml");
        }
    }
}
