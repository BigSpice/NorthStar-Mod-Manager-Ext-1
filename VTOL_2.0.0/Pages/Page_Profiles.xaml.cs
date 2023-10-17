﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO.Compression;
using System.Threading;
using System.Text.RegularExpressions;
using HandyControl.Tools;
using System.Windows.Media.Animation;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Globalization;
using Windows.System.Profile;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Serilog;

namespace VTOL.Pages
		{

	/// <summary>
	/// Interaction logic for Page_Profiles.xaml
	/// </summary>
	/// 
	namespace FileLockInfo
	{
		public static class Win32Processes
		{
			/// <summary>
			/// Find out what process(es) have a lock on the specified file.
			/// </summary>
			/// <param name="path">Path of the file.</param>
			/// <returns>Processes locking the file</returns>
			/// <remarks>See also:
			/// http://msdn.microsoft.com/en-us/library/windows/desktop/aa373661(v=vs.85).aspx
			/// http://wyupdate.googlecode.com/svn-history/r401/trunk/frmFilesInUse.cs (no copyright in code at time of viewing)
			/// </remarks>
			public static List<Process> GetProcessesLockingFile(string path)
			{
				uint handle;
				string key = Guid.NewGuid().ToString();
				int res = RmStartSession(out handle, 0, key);

				if (res != 0) throw new Exception("Could not begin restart session.  Unable to determine file locker.");

				try
				{
					const int MORE_DATA = 234;
					uint pnProcInfoNeeded, pnProcInfo = 0, lpdwRebootReasons = RmRebootReasonNone;

					string[] resources = { path }; // Just checking on one resource.

					res = RmRegisterResources(handle, (uint)resources.Length, resources, 0, null, 0, null);

					if (res != 0) throw new Exception("Could not register resource.");

					//Note: there's a race condition here -- the first call to RmGetList() returns
					//      the total number of process. However, when we call RmGetList() again to get
					//      the actual processes this number may have increased.
					res = RmGetList(handle, out pnProcInfoNeeded, ref pnProcInfo, null, ref lpdwRebootReasons);

					if (res == MORE_DATA)
					{
						return EnumerateProcesses(pnProcInfoNeeded, handle, lpdwRebootReasons);
					}
					else if (res != 0) throw new Exception("Could not list processes locking resource. Failed to get size of result.");
				}
				finally
				{
					RmEndSession(handle);
				}

				return new List<Process>();
			}


			[StructLayout(LayoutKind.Sequential)]
			public struct RM_UNIQUE_PROCESS
			{
				public int dwProcessId;
				public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
			}

			const int RmRebootReasonNone = 0;
			const int CCH_RM_MAX_APP_NAME = 255;
			const int CCH_RM_MAX_SVC_NAME = 63;

			public enum RM_APP_TYPE
			{
				RmUnknownApp = 0,
				RmMainWindow = 1,
				RmOtherWindow = 2,
				RmService = 3,
				RmExplorer = 4,
				RmConsole = 5,
				RmCritical = 1000
			}

			[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
			public struct RM_PROCESS_INFO
			{
				public RM_UNIQUE_PROCESS Process;

				[MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_APP_NAME + 1)] public string strAppName;

				[MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_SVC_NAME + 1)] public string strServiceShortName;

				public RM_APP_TYPE ApplicationType;
				public uint AppStatus;
				public uint TSSessionId;
				[MarshalAs(UnmanagedType.Bool)] public bool bRestartable;
			}

			[DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
			static extern int RmRegisterResources(uint pSessionHandle, uint nFiles, string[] rgsFilenames,
				uint nApplications, [In] RM_UNIQUE_PROCESS[] rgApplications, uint nServices,
				string[] rgsServiceNames);

			[DllImport("rstrtmgr.dll", CharSet = CharSet.Auto)]
			static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

			[DllImport("rstrtmgr.dll")]
			static extern int RmEndSession(uint pSessionHandle);

			[DllImport("rstrtmgr.dll")]
			static extern int RmGetList(uint dwSessionHandle, out uint pnProcInfoNeeded,
				ref uint pnProcInfo, [In, Out] RM_PROCESS_INFO[] rgAffectedApps,
				ref uint lpdwRebootReasons);

			private static List<Process> EnumerateProcesses(uint pnProcInfoNeeded, uint handle, uint lpdwRebootReasons)
			{
				var processes = new List<Process>(10);
				// Create an array to store the process results
				var processInfo = new RM_PROCESS_INFO[pnProcInfoNeeded];
				var pnProcInfo = pnProcInfoNeeded;

				// Get the list
				var res = RmGetList(handle, out pnProcInfoNeeded, ref pnProcInfo, processInfo, ref lpdwRebootReasons);

				if (res != 0) throw new Exception("Could not list processes locking resource.");
				for (int i = 0; i < pnProcInfo; i++)
				{
					try
					{
						processes.Add(Process.GetProcessById(processInfo[i].Process.dwProcessId));
					}
					catch (ArgumentException) { } // catch the error -- in case the process is no longer running
				}
				return processes;
			}
		}
	}
	public partial class Page_Profiles : Page
	{


		public MainWindow Main = GetMainWindow();
		string NAME__;
		int NUMBER_MODS__;
		string SIZE;
		string VERSION;
		string SAVE_NAME__;
		string SAVE_PATH__;
		string CURRENT_FILE__;
		List<Card_> Final_List = new List<Card_>();
		public bool _Completed_Mod_call = false;

		bool Do_Not_save_Mods = false;
		string[] Folders = new string[] { @"R2Northstar\plugins", @"R2Northstar\packages", @"bin\x64_dedi" };
		string[] Files = new string[] { "Northstar.dll", "NorthstarLauncher.exe", "r2ds.bat", "discord_game_sdk.dll" , "wsock32.dll", "ns_startup_args.txt", "ns_startup_args_dedi.txt", "placeholder_playerdata.pdata", "LEGAL.txt" };
		bool Skip_Mods = false;
		bool Backup_Profile_Current = false;
		bool cancel = false;
		public CancellationTokenSource _cts = new CancellationTokenSource();
		public class PathModel
		{
			public string PathName { get; set; }
			public string Path { get; set; }
		}
		class GZipStreamWithProgress : GZipStream
		{
			public event EventHandler<double> ProgressChanged;
			public event EventHandler<string> CurrentFileChanged;

			private long totalBytesRead;
			private string currentFile;

			public GZipStreamWithProgress(Stream stream, CompressionMode mode) : base(stream, mode) { }

			public override int Read(byte[] buffer, int offset, int count)
			{
				int bytesRead = base.Read(buffer, offset, count);
				totalBytesRead += bytesRead;

				OnProgressChanged(totalBytesRead);
				//Console.WriteLine(bytesRead);
				return bytesRead;
			}

			public void SetCurrentFile(string file)
			{
				currentFile = file;
				OnCurrentFileChanged(file);
			}

			protected void OnProgressChanged(long totalBytesRead)
			{
				double progress = (double)totalBytesRead / BaseStream.Length;
				ProgressChanged?.Invoke(this, progress);
			}

			protected void OnCurrentFileChanged(string file)
			{
				CurrentFileChanged?.Invoke(this, file);
			}
		}
		public async void UnpackRead_BIN_INFO(string path)
		{
			try

			{
				CURRENT_FILE__ = path;
				string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

				//decompress the .vbp file
				using (FileStream sourceStream = File.Open(path, FileMode.Open))
				{


					if (sourceStream.Length == 0)
					{
						return;
					}
					using (var decompressionStream = new GZipStreamWithProgress(sourceStream, CompressionMode.Decompress))
					{

						if (File.Exists(appDataPath + @"\File_info.bin"))
						{
							File.Delete(appDataPath + @"\File_info.bin");
						}
						// unpack the "directory.bin" file
						using (FileStream decompressedFileStream = new FileStream(appDataPath + @"\File_info.bin", FileMode.Create))
						{
							decompressionStream.CopyTo(decompressedFileStream);
						}

						using (var stream = File.Open(appDataPath + @"\File_info.bin", FileMode.Open))
						{
							var formatter = new BinaryFormatter();
							var data = (DirectoryData)formatter.Deserialize(stream);
							string dataAsString = data.ToString();
							//string dataAsString = data.ToString();
							if (data != null)
							{
								if (data.NAME.Length > 1 && data.NORTHSTAR_VERSION.Length > 1 && data.TOTAL_SIZE_OF_FOLDERS.Length > 1)
								{
									I_NORTHSTAR_VERSION.Content = data.NORTHSTAR_VERSION;
									I_NUMBER_OF_MODS.Content = data.MOD_COUNT;
									I_TOTAL_SIZE.Content = data.TOTAL_SIZE_OF_FOLDERS;
									NAME.Content = data.NAME;

									//Console.WriteLine(data.NAME + "\n" + data.NORTHSTAR_VERSION + "\n" + data.MOD_COUNT + "\n" + data.TOTAL_SIZE_OF_FOLDERS);
									DispatchIfNecessary(async () =>
							{
								Main.Profile_TAG.Content = data.NAME;
								Properties.Settings.Default.Profile_Name = data.NAME;
								Properties.Settings.Default.Save();


							});
								}
							}
						}

					}
				}


				if (File.Exists(appDataPath + @"\File_info.bin"))
				{
					File.Delete(appDataPath + @"\File_info.bin");
				}

			}
			catch (Exception ex)
			{
				Log.Error(ex, $"A crash happened at {DateTime.Now.ToString("yyyy - MM - dd HH - mm - ss.ff", CultureInfo.InvariantCulture)}{Environment.NewLine}");
			}
		}


		public bool UnpackDirectory(string path, string targetDirectory, CancellationToken token)
		{
			try
			{


				string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
				if (File.Exists(appDataPath + @"\directory_open.bin"))
				{

					TryDeleteFile(appDataPath + @"\directory_open.bin");
				}
				//decompress the bin.gz file
				using (FileStream sourceStream = File.Open(path, FileMode.Open))
				{


					if (sourceStream.Length == 0)
					{
						//Console.WriteLine("The file is empty.");
						return false;
					}
					using (var decompressionStream = new GZipStreamWithProgress(sourceStream, CompressionMode.Decompress))
					{


						// unpack the "directory.bin" file
						using (FileStream decompressedFileStream = new FileStream(appDataPath + @"\directory_open.bin", FileMode.Create))
						{
							decompressionStream.CopyTo(decompressedFileStream);
							decompressionStream.Close();
						}

						decompressionStream.Close();

					}
					
					// unpack the "directory.bin" file
					using (var stream = File.Open(appDataPath + @"\directory_open.bin", FileMode.OpenOrCreate))
					{
						var formatter = new BinaryFormatter();
						var data = (DirectoryData)formatter.Deserialize(stream);
						string dataAsString = data.ToString();
						if (Skip_Mods == true)
						{
							data.Folders = data.Folders.Where(folder => !folder.Contains("R2Northstar\\packages")).ToArray();
							data.Files = data.Files.Where(file => !file.Path.Contains("R2Northstar\\packages")).ToArray();

						}
						if(Backup_Profile_Current == true)
                        {

							if (Directory.Exists(Main.User_Settings_Vars.NorthstarInstallLocation + "VTOL_profiles\\"))
							{
								DispatchIfNecessary(async () =>
								{
								Main.Snackbar.Appearance = Wpf.Ui.Common.ControlAppearance.Info;
								Main.Snackbar.Show("INFO", "Backing Up Current Profile now");
								Pack_Label.Content = "Packing the File/Folder";
								});
											
								
								Task packTask = Task.Run(() => Pack_NO_UI(Main.User_Settings_Vars.NorthstarInstallLocation, Folders, Files));
								// Wait for the task to complete before continuing
								packTask.Wait();

								DispatchIfNecessary(async () =>
								{
									LoadProfiles();
								});
								//TryCopyFile(Name_, Main.User_Settings_Vars.NorthstarInstallLocation + "VTOL_profiles\\" + f.Name);
							}

						}
						double totalSize = data.Folders.Count() + data.Files.Count();
						double currentSize = 0;
						string currentitem = "";


						foreach (string folder in data.Folders)
						{
							if (token.IsCancellationRequested || cancel == true)
							{
								return false;
							}
							string append = "";


							string foldername = System.IO.Path.GetFileName(folder);
							currentitem = foldername;
							int index = folder.LastIndexOf("Titanfall2");
							string fileNameUpToWord = folder.Substring(index + "Titanfall2".Length + 1);
							string targetFolder = System.IO.Path.Combine(targetDirectory, fileNameUpToWord);
							Directory.CreateDirectory(targetFolder);
							//	System.Threading.Thread.Sleep(50); // to simulate delay
							currentSize++;
							double progress = currentSize / totalSize * 100;
							int progressInt = (int)Math.Round(progress);
							DispatchIfNecessary(async () =>
							{
								Current_File_Tag.Content = append + currentitem;
								wave_progress.Text = progressInt + "%";
								wave_progress.Value = progressInt;

							});
							if (token.IsCancellationRequested || cancel == true)
							{
								return false;
							}
							//Console.WriteLine("Creating Folders " + progressInt + "% - " + currentitem);
						}

						foreach (FileData file in data.Files)
						{
							if (token.IsCancellationRequested || cancel == true)
							{
								return false;
							}
							string append = "";
							string fileName = System.IO.Path.GetFileName(file.Path);
							currentitem = fileName;
							string targetFile = System.IO.Path.Combine(targetDirectory, fileName);
							string parentFolder = System.IO.Path.GetDirectoryName(file.Path);
							int index = file.Path.LastIndexOf("Titanfall2");
							string fileNameUpToWord = file.Path.Substring(index + "Titanfall2".Length + 1);

							TryCreateFile(file.Data, System.IO.Path.Combine(targetDirectory, fileNameUpToWord), true);
							//	else
							//{
							//}
							//	System.Threading.Thread.Sleep(50); // to simulate delay
							currentSize++;
							double progress = currentSize / totalSize * 100;
							int progressInt = (int)Math.Round(progress);
							DispatchIfNecessary(async () =>
							{
								Current_File_Tag.Content = append + currentitem;
								wave_progress.Text = progressInt + "%";
								wave_progress.Value = progressInt;

							});
							if (token.IsCancellationRequested || cancel == true)
							{
								return false;
							}

							//Console.WriteLine("Copying... " + progressInt + "% - " + currentitem);
						}
						CheckDirectory(appDataPath + @"\directory_open.bin", targetDirectory);
					}


				}
				if (File.Exists(appDataPath + @"\directory_open.bin"))
				{
					File.Delete(appDataPath + @"\directory_open.bin");
				}

				return true;

			}
			catch (FileNotFoundException ex)
			{
				//Console.WriteLine("The file could not be found: " + ex.Message);
				return false;

			}
			catch (DirectoryNotFoundException ex)
			{
				//Console.WriteLine("The directory could not be found: " + ex.Message);
				return false;

			}
			catch (IOException ex)
			{
				//Console.WriteLine("An IO error occurred: " + ex.Message);
				return false;

			}
			catch (Exception ex)
			{
				Log.Error(ex, $"A crash happened at {DateTime.Now.ToString("yyyy - MM - dd HH - mm - ss.ff", CultureInfo.InvariantCulture)}{Environment.NewLine}");
				return false;

			}
			return false;

		}
		public async Task<bool> TryDeleteDirectoryAsync(string directoryPath, bool overwrite = true, int maxRetries = 10, int millisecondsDelay = 30)
		{
			if (directoryPath == null)
				throw new ArgumentNullException(directoryPath);
			if (maxRetries < 1)
				throw new ArgumentOutOfRangeException(nameof(maxRetries));
			if (millisecondsDelay < 1)
				throw new ArgumentOutOfRangeException(nameof(millisecondsDelay));

			for (int i = 0; i < maxRetries; ++i)
			{
				try
				{
					if (Directory.Exists(directoryPath))
					{
						Directory.Delete(directoryPath, overwrite);
					}

					return true;
				}
				catch (IOException)
				{
					Thread.Sleep(millisecondsDelay);
				}
				catch (UnauthorizedAccessException)
				{
					Thread.Sleep(millisecondsDelay);
				}
			}

			return false;
		}
		public bool TryDeleteDirectory(string directoryPath, bool overwrite = true, int maxRetries = 10, int millisecondsDelay = 300)
		{
			if (directoryPath == null)
				throw new ArgumentNullException(directoryPath);
			if (maxRetries < 1)
				throw new ArgumentOutOfRangeException(nameof(maxRetries));
			if (millisecondsDelay < 1)
				throw new ArgumentOutOfRangeException(nameof(millisecondsDelay));

			for (int i = 0; i < maxRetries; ++i)
			{
				try
				{
					if (Directory.Exists(directoryPath))
					{
						Directory.Delete(directoryPath, overwrite);
					}

					return true;
				}
				catch (IOException)
				{
					Thread.Sleep(millisecondsDelay);
				}
				catch (UnauthorizedAccessException)
				{
					Thread.Sleep(millisecondsDelay);
				}
			}

			return false;
		}
		public bool TryCreateDirectory(string directoryPath, int maxRetries = 10, int millisecondsDelay = 200)
		{
			if (directoryPath == null)
				throw new ArgumentNullException(directoryPath);
			if (maxRetries < 1)
				throw new ArgumentOutOfRangeException(nameof(maxRetries));
			if (millisecondsDelay < 1)
				throw new ArgumentOutOfRangeException(nameof(millisecondsDelay));

			for (int i = 0; i < maxRetries; ++i)
			{
				try
				{

					Directory.CreateDirectory(directoryPath);

					if (Directory.Exists(directoryPath))
					{

						return true;
					}


				}
				catch (IOException)
				{
					Thread.Sleep(millisecondsDelay);
				}
				catch (UnauthorizedAccessException)
				{
					Thread.Sleep(millisecondsDelay);
				}
			}

			return false;
		}
		protected virtual bool IsFileLocked(string File)
		{
			try
			{
				FileInfo file = new FileInfo(File);
				using (FileStream stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None))
				{
					stream.Close();
				}
			}
			catch (IOException)
			{
				//the file is unavailable because it is:
				//still being written to
				//or being processed by another thread
				//or does not exist (has already been processed)
				return true;
			}

			//file is not locked
			return false;
		}
		public bool TryMoveFile(string Origin, string Destination, bool overwrite = true, int maxRetries = 10, int millisecondsDelay = 200)
		{
			if(IsFileLocked(Origin) || IsFileLocked(Destination))
				return false;
			if (Origin == null)
				throw new ArgumentNullException(Origin);
			if (maxRetries < 1)
				throw new ArgumentOutOfRangeException(nameof(maxRetries));
			if (millisecondsDelay < 1)
				throw new ArgumentOutOfRangeException(nameof(millisecondsDelay));

			for (int i = 0; i < maxRetries; ++i)
			{
				try
				{
					if (File.Exists(Origin))
					{
						File.Move(Origin, Destination, overwrite);
					}

					return true;
				}
				catch (IOException)
				{
					Thread.Sleep(millisecondsDelay);
				}
				catch (UnauthorizedAccessException)
				{
					Thread.Sleep(millisecondsDelay);
				}
			}

			return false;
		}
	
		public bool TryCopyFile(string Origin, string Destination, bool overwrite = true, int maxRetries = 10, int millisecondsDelay = 300)
		{
			
			if (Origin == null)
				throw new ArgumentNullException(Origin);
			if (maxRetries < 1)
				throw new ArgumentOutOfRangeException(nameof(maxRetries));
			if (millisecondsDelay < 1)
				throw new ArgumentOutOfRangeException(nameof(millisecondsDelay));

			for (int i = 0; i < maxRetries; ++i)
			{
				try


				{
					string directoryPath = System.IO.Path.GetDirectoryName(Destination);
					if (!Directory.Exists(directoryPath)){

						Directory.CreateDirectory(directoryPath);
                    }
				
					if (File.Exists(Origin))
					{
						
						File.Copy(Origin, Destination, true);

					}
					Thread.Sleep(millisecondsDelay);

					return true;
				}
				catch (IOException)
				{
					

					Thread.Sleep(millisecondsDelay);
				}
				catch (UnauthorizedAccessException)
				{
					Thread.Sleep(millisecondsDelay);
				}
			}

			return false;
		}

		public bool TryCreateFile(byte[] origin, string destination, bool overwrite = true, int maxRetries = 10, int millisecondsDelay = 300)
		{
			if (origin == null)
				throw new ArgumentNullException(nameof(origin));
			if (maxRetries < 1)
				throw new ArgumentOutOfRangeException(nameof(maxRetries));
			if (millisecondsDelay < 1)
				throw new ArgumentOutOfRangeException(nameof(millisecondsDelay));

			for (int i = 0; i < maxRetries; ++i)
			{
				try
				{
					string directoryPath = System.IO.Path.GetDirectoryName(destination);
					if (!Directory.Exists(directoryPath))
					{
						Directory.CreateDirectory(directoryPath);
					}

					using (var stream = new FileStream(destination, overwrite ? FileMode.Create : FileMode.CreateNew))
					{
						stream.Write(origin, 0, origin.Length);
					}

					return true;
				}
				catch (IOException)
				{
					Thread.Sleep(millisecondsDelay);
				}
				catch (UnauthorizedAccessException)
				{
					Thread.Sleep(millisecondsDelay);
				}
			}

			return false;
		}
		public bool TryDeleteFile(
			string Origin,
			int maxRetries = 10,
			int millisecondsDelay = 300)
		{
			if (Origin == null)
				throw new ArgumentNullException(Origin);
			if (maxRetries < 1)
				throw new ArgumentOutOfRangeException(nameof(maxRetries));
			if (millisecondsDelay < 1)
				throw new ArgumentOutOfRangeException(nameof(millisecondsDelay));

			for (int i = 0; i < maxRetries; ++i)
			{
				try
				{
					if (File.Exists(Origin))
					{
						File.Delete(Origin);
					}
					Thread.Sleep(millisecondsDelay);

					return true;
				}
				catch (IOException)
				{
					Thread.Sleep(millisecondsDelay);
				}
				catch (UnauthorizedAccessException)
				{
					Thread.Sleep(millisecondsDelay);
				}
			}

			return false;
		}
		public void DispatchIfNecessary(Action action)
		{
			if (!Dispatcher.CheckAccess())
				Dispatcher.Invoke(action);
			else
				action.Invoke();
		}
		public class FileSizeCalculator
		{
			public static string Pack(string directoryPath, string[] folders, string[] files)
			{
				long totalSize = 0;
				// check if the directory exists
				if (Directory.Exists(directoryPath))
				{
					// get the directory info
					DirectoryInfo directory = new DirectoryInfo(directoryPath);
					// iterate through the folders array
					foreach (string folder in folders)
					{
						// check if the folder exists in the directory
						if (directory.GetDirectories(folder).Length > 0)
						{
							// get the folder info
							DirectoryInfo folderPath = directory.GetDirectories(folder)[0];
							// get the folder size
							totalSize += GetDirectorySize(folderPath);
						}
					}
					// iterate through the files array
					foreach (string file in files)
					{
						// check if the file exists in the directory
						if (directory.GetFiles(file).Length > 0)
						{
							// get the file info
							FileInfo filePath = directory.GetFiles(file)[0];
							// get the file size
							totalSize += filePath.Length;
						}
					}
				}
				return FormatSizeUnits(totalSize);
			}

			private static long GetDirectorySize(DirectoryInfo directory)
			{
				long totalSize = 0;
				// get the size of the files in the directory
				foreach (FileInfo file in directory.GetFiles())
				{
					totalSize += file.Length;
				}
				// get the size of the subdirectories
				foreach (DirectoryInfo subDirectory in directory.GetDirectories())
				{
					totalSize += GetDirectorySize(subDirectory);
				}
				return totalSize;
			}
			private static string FormatSizeUnits(long bytes)
			{
				string[] units = { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
				double mod = 1024.0;
				int i = 0;
				while (bytes >= mod)
				{
					bytes /= (long)mod;
					i++;
				}
				return $"{bytes:F2} {units[i]}";
			}
		}
		public string[] CheckAndRemoveMissingFolders(string[] folderNames)
		{
			return folderNames.Where(folder => Directory.Exists(folder)).ToArray();
		}
		public string[] CheckAndRemoveMissingFilesAndFolders(string[] fileAndFolderNames)
		{
			try
			{

				var validFilesAndFolders = new List<string>();
				int totalFiles = fileAndFolderNames.Length;
				int currentFile = 0;
				//	Console.Write("Checking files and folders ---> 0%");
				//	Console.CursorVisible = false; // to hide the cursor
				foreach (string fileOrFolderName in fileAndFolderNames)
				{
					currentFile++;
					int progress = (currentFile * 100) / totalFiles;
					//		Console.SetCursorPosition(23, Console.CursorTop); // to set cursor position
					//	Console.Write(progress + "%");
					if (File.Exists(fileOrFolderName) || Directory.Exists(fileOrFolderName))
					{
						validFilesAndFolders.Add(fileOrFolderName);
					}

				}
				return validFilesAndFolders.ToArray();
			}
			catch (Exception ex)
			{
				Log.Error(ex, $"A crash happened at {DateTime.Now.ToString("yyyy - MM - dd HH - mm - ss.ff", CultureInfo.InvariantCulture)}{Environment.NewLine}");
			}
			return null;
		}

		public string CheckAndRemoveMissingFileAndFolder(string fileOrFolderPath)
		{
			try
			{
				var validFilesAndFolders = new List<string>();

				if (File.Exists(fileOrFolderPath) || Directory.Exists(fileOrFolderPath))
				{
					return fileOrFolderPath;
				}

				
			}
			catch (Exception ex)
			{
				Log.Error(ex, $"A crash happened at {DateTime.Now.ToString("yyyy - MM - dd HH - mm - ss.ff", CultureInfo.InvariantCulture)}{Environment.NewLine}");
			}
			return null;
		}
		public  void CheckDirectory(string binFilePath, string targetPath)
		{
            try { 
			if (!Directory.Exists(binFilePath) || !Directory.Exists(targetPath)) {


				return;
			}
			// Deserialize the directory data from the bin file
			DirectoryData data;
			using (var stream = File.OpenRead(binFilePath))
			{
				var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
				data = (DirectoryData)formatter.Deserialize(stream);
			}

			var missingFolders = new List<string>();
			var missingFiles = new List<string>();
			string dataAsString = data.ToString();

			// Check if folders exist
			foreach (var folder in data.Folders)
			{

				int index = folder.LastIndexOf("Titanfall2");
				string fileNameUpToWord = folder.Substring(index + "Titanfall2".Length + 1);
				var targetFolder = System.IO.Path.Combine(targetPath, fileNameUpToWord);

				if (!Directory.Exists(targetFolder))
				{
					missingFolders.Add(targetFolder);
				}
			}

			// Check if files exist
			foreach (var file in data.Files)
			{
				int index = file.Path.LastIndexOf("Titanfall2");
				string fileNameUpToWord = file.Path.Substring(index + "Titanfall2".Length + 1);
				var targetFile = System.IO.Path.Combine(targetPath, fileNameUpToWord);

				if (!File.Exists(targetFile))
				{
					missingFiles.Add(targetFile);
				}
			}

			// Display summary of missing folders and files
			var message = "";
			if (missingFolders.Count > 0)
			{
				message += $"Missing Folders: {string.Join(", ", missingFolders)}\n";
			}
			if (missingFiles.Count > 0)
			{
				message += $"Missing Files: {string.Join(", ", missingFiles)}\n";
			}

			//if (!string.IsNullOrEmpty(message))
			//{
			//	//MessageBox.Show(message, "Missing Folders and Files");
			//}
			//else
			//{

			//	//MessageBox.Show("Files Verified Successfully!");

			//}
		}
			catch (Exception ex)
			{
				Log.Error(ex, $"A crash happened at {DateTime.Now.ToString("yyyy - MM - dd HH - mm - ss.ff", CultureInfo.InvariantCulture)}{Environment.NewLine}");
			}
		}
		public static string[] TrimPathsToTitanfall2(string[] paths)
		{
			string titanfall2FolderName = "Titanfall2" + System.IO.Path.DirectorySeparatorChar;
			string[] resultArray = new string[paths.Length];

			for (int i = 0; i < paths.Length; i++)
			{
				string path = paths[i];

				// Check if the path contains the Titanfall2 folder
				int titanfall2Index = path.IndexOf(titanfall2FolderName, StringComparison.OrdinalIgnoreCase);
				if (titanfall2Index < 0)
				{
					// Titanfall2 folder not found in path, return the original path
					resultArray[i] = path;
				}
				else
				{
					// Trim the path to start at the Titanfall2 folder
					int startIndex = titanfall2Index + titanfall2FolderName.Length;
					string trimmedPath = path.Substring(startIndex);

					// Add the Titanfall2 folder to the trimmed path
					trimmedPath = System.IO.Path.Combine(titanfall2FolderName, trimmedPath);

					resultArray[i] = trimmedPath;
				}
			}

			return resultArray;
		}
		public static string TrimPathToTitanfall2(string path)
		{
			string titanfall2FolderName = "Titanfall2" + System.IO.Path.DirectorySeparatorChar;

			// Check if the path contains the Titanfall2 folder
			int titanfall2Index = path.IndexOf(titanfall2FolderName, StringComparison.OrdinalIgnoreCase);
			if (titanfall2Index < 0)
			{
				// Titanfall2 folder not found in path, return the original path
				return path;
			}
			else
			{
				// Trim the path to start at the Titanfall2 folder
				int startIndex = titanfall2Index + titanfall2FolderName.Length;
				string trimmedPath = path.Substring(startIndex);

				// Add the Titanfall2 folder to the trimmed path
				trimmedPath = System.IO.Path.Combine(titanfall2FolderName, trimmedPath);

				return trimmedPath;
			}
		}
		public bool ListDirectory(string path, string[] includedFolders, string[] includedFiles, CancellationToken token)
		{
			try {

				if (!Directory.Exists(SAVE_PATH__))
				{
					Main.Snackbar.Appearance = Wpf.Ui.Common.ControlAppearance.Caution;
					Main.Snackbar.ShowAsync("ERROR!", "The Profile Path" + SAVE_PATH__ + "Is Invalid!");
					return false;
				}

				//Console.WriteLine("Starting");
				var allFolders = Directory.GetDirectories(path, "*", SearchOption.AllDirectories);
				var allFiles = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
				IEnumerable<string> includedFoldersPath = Enumerable.Empty<string>();
				IEnumerable<string> includedFilesPath = Enumerable.Empty<string>();
				if (token.IsCancellationRequested)
					return false;
				
				

				if (Do_Not_save_Mods == true)
				{
					//string NS_Mod_Dir = Main.User_Settings_Vars.NorthstarInstallLocation + @"R2Northstar\packages";
					//Console.WriteLine("Skipped Mods");

					includedFoldersPath = allFolders.Where(f => !f.Contains("R2Northstar\\packages") && includedFolders.Any(i => f.StartsWith(System.IO.Path.Combine(path, i))));

					includedFilesPath = allFiles.Where(f => !f.Contains("R2Northstar\\packages") && includedFiles.Contains(System.IO.Path.GetFileName(f)) || includedFoldersPath.Any(folder => f.StartsWith(folder)));

				}
				else
				{
					includedFoldersPath = allFolders.Where(f => includedFolders.Any(i => f.StartsWith(System.IO.Path.Combine(path, i))));
					includedFilesPath = allFiles.Where(f => includedFiles.Contains(System.IO.Path.GetFileName(f)) || includedFoldersPath.Any(folder => f.StartsWith(folder)));

				}
				if (token.IsCancellationRequested)
					return false;
				//Console.WriteLine("\n\n\n\n\n FILES \n\n\n\n\n");
				//Console.WriteLine(string.Join("\n ", includedFilesPath.ToArray()));
				//Console.WriteLine("\n\n\n\n\n FILES IN DATA \n\n\n\n\n");
				//Console.WriteLine(string.Join("\n ", TrimPathsToTitanfall2(includedFilesPath.ToArray())));

				var data = new DirectoryData
				{
					
					Folders = TrimPathsToTitanfall2(CheckAndRemoveMissingFilesAndFolders(includedFoldersPath.Select(f => f).ToArray())),
					Files = includedFilesPath.Select(f => new FileData { Path = TrimPathToTitanfall2(CheckAndRemoveMissingFileAndFolder(f)), Data = File.ReadAllBytes(f) }).ToArray(),
					NORTHSTAR_VERSION = VERSION,
					NAME = SAVE_NAME__,
					MOD_COUNT = NUMBER_MODS__,
					TOTAL_SIZE_OF_FOLDERS = SIZE

				};
				//foreach(var f in data.Files)
    //            {

				//	Console.WriteLine("\n" + f.Path);
				//	Console.WriteLine("\n" + f.Data.Length.ToString());


				//}
				if (token.IsCancellationRequested)
					return false;

				using (var stream = File.Open(SAVE_PATH__ + @"\" + EnforceWindowsStringName(SAVE_NAME__) + ".bin", FileMode.Create))
				{
					var formatter = new BinaryFormatter();
					formatter.Serialize(stream, data);
				}
				if (token.IsCancellationRequested)
					return false;
				//compress the bin file
				using (FileStream sourceStream = File.Open(SAVE_PATH__ + @"\" + EnforceWindowsStringName(SAVE_NAME__) + ".bin", FileMode.Open))
				using (FileStream targetStream = File.Create(SAVE_PATH__ + @"\" + EnforceWindowsStringName(SAVE_NAME__) + ".vbp"))
				using (GZipStreamWithProgress compressionStream = new GZipStreamWithProgress(targetStream, CompressionMode.Compress))
				{
					sourceStream.CopyTo(compressionStream);
				}





				if (File.Exists(SAVE_PATH__ + @"\" + EnforceWindowsStringName(SAVE_NAME__) + ".bin"))
				{

					File.Delete(SAVE_PATH__ + @"\" + EnforceWindowsStringName(SAVE_NAME__) + ".bin");

				}
				cancel = false;

				CancelWork();
				return true;



			}
			catch (OperationCanceledException)
			{
				//Console.WriteLine("Cancelled!");
				return false;

			}
			catch (Exception ex)
			{
				Log.Error(ex, $"A crash happened at {DateTime.Now.ToString("yyyy - MM - dd HH - mm - ss.ff", CultureInfo.InvariantCulture)}{Environment.NewLine}");
				return false;
			}

			return false;

		}






		[Serializable]
		public class DirectoryData
		{
			public string[] Folders { get; set; }
			public FileData[] Files { get; set; }
			public string NORTHSTAR_VERSION { get; set; }
			public string NAME { get; set; }
			public int MOD_COUNT { get; set; }
			public string TOTAL_SIZE_OF_FOLDERS { get; set; }

			public override string ToString()
			{
				var paths = Folders.Concat(Files.Select(f => f.Path));
				return string.Join(" ", paths);
			}
		}
		[Serializable]
		public class FileData
		{
			public string Path { get; set; }
			public byte[] Data { get; set; }
		}

		public string EnforceWindowsStringName(string input)
		{
			// Replace spaces with underscores
			input = input.Replace(" ", "_");

			// Remove any invalid characters
			input = Regex.Replace(input, @"[^a-zA-Z0-9_]", "");

			// Remove any leading or trailing underscores
			input = input.Trim('_');

			// Ensure the first character is a letter or number
			if (!char.IsLetterOrDigit(input[0]))
			{
				input = "_" + input;
			}

			return input;
		}
		public void FadeControl(Control control, bool fadeIn, double duration = 1)
		{
			if (fadeIn)
			{
				control.Visibility = Visibility.Visible;
				var animation = new DoubleAnimation
				{
					From = 0,
					To = 1,
					Duration = new Duration(TimeSpan.FromSeconds(duration))
				};
				control.BeginAnimation(UIElement.OpacityProperty, animation);
			}
			else
			{
				var animation = new DoubleAnimation
				{
					From = 1,
					To = 0,
					Duration = new Duration(TimeSpan.FromSeconds(duration))
				};
				animation.Completed += (sender, e) => control.Visibility = Visibility.Hidden;
				control.BeginAnimation(UIElement.OpacityProperty, animation);
			}
		}
		private void Pack_NO_UI(string target_directory, string[] folders_, string[] files_)
        {
			_cts = new CancellationTokenSource();
			var token = _cts.Token;

			bool result = false;
			string NS_Mod_Dir = Main.User_Settings_Vars.NorthstarInstallLocation + @"R2Northstar\packages";

			if (Directory.Exists(target_directory) && Directory.Exists(NS_Mod_Dir))
			{
				System.IO.DirectoryInfo rootDirs = new DirectoryInfo(@NS_Mod_Dir);
				System.IO.DirectoryInfo[] subDirs = null;
				subDirs = rootDirs.GetDirectories();
				string currentDateTimeString = DateTime.Now.ToString("yyyyMMdd-HHmmss");
				VERSION = Main.User_Settings_Vars.CurrentVersion;
				NUMBER_MODS__ = subDirs.Length;
				SIZE = FileSizeCalculator.Pack(Main.User_Settings_Vars.NorthstarInstallLocation, Folders, Files);
				SAVE_NAME__ = "BACKUP - " + currentDateTimeString;
				SAVE_PATH__ = Main.User_Settings_Vars.NorthstarInstallLocation + "VTOL_profiles\\";

				while (!token.IsCancellationRequested)
				{
					// Check the token's cancellation status
					token.ThrowIfCancellationRequested();
					// Add your long running operation here
					result = ListDirectory(target_directory, folders_, files_, token);
				}


				if (result == true)
				{
					DispatchIfNecessary(async () =>
					{
						Main.Snackbar.Appearance = Wpf.Ui.Common.ControlAppearance.Success;
						Main.Snackbar.Show("SUCCESS!", "The Profile " + SAVE_NAME__ + " has been created");
						cancel = false;

					});
				}
				else
				{
					DispatchIfNecessary(async () =>
					{
						Main.Snackbar.Appearance = Wpf.Ui.Common.ControlAppearance.Caution;
						Main.Snackbar.Show(VTOL.Resources.Languages.Language.ERROR, "The Backup Failed!");
					});
				}
			}

		}

		private async void Pack(string target_directory, string[] folders_, string[] files_)
        {
			try
			{
				_cts = new CancellationTokenSource();
				var token = _cts.Token;
				
					DispatchIfNecessary(async () =>
				{
					// Perform the long-running operation on a background thread
					try
					{
						bool result = false;
						var result_ = await Task.Run(() =>
						{
							while (!token.IsCancellationRequested)
							{
								// Check the token's cancellation status
								token.ThrowIfCancellationRequested();
								// Add your long running operation here
								result = ListDirectory(target_directory, folders_, files_, token);
							}
							return result;
						}, token);
						if (result == true)
						{
							//Console.WriteLine(result);
							//Console.WriteLine("Complete!");
							Main.Snackbar.Appearance = Wpf.Ui.Common.ControlAppearance.Success;
							Main.Snackbar.Show("SUCCESS!", "The Profile " + SAVE_NAME__ + "Has Been Packed");
							Loading_Panel.Visibility = Visibility.Hidden;
							wave_progress.Visibility = Visibility.Hidden;
							Circe_progress.Visibility = Visibility.Hidden;
							Options_Panel.Visibility = Visibility.Hidden;
							Add_Profile_Options_Panel.Visibility = Visibility.Hidden;
							Export_Profile_Options_Panel.Visibility = Visibility.Hidden;
							cancel = false;

							DispatchIfNecessary(async () =>
							{
								LoadProfiles();
								//PopulateListBoxWithRandomPaths();
							});
						}
						else
						{
							//Console.WriteLine(result);
							//Console.WriteLine("Failed!");
							Main.Snackbar.Appearance = Wpf.Ui.Common.ControlAppearance.Caution;
							Main.Snackbar.Show(VTOL.Resources.Languages.Language.ERROR, "The Profile " + SAVE_NAME__ + "Failed To Be Packed");
						}
					}
					catch (OperationCanceledException)
					{
						// Handle the cancellation
						//Console.WriteLine("Cancelled!");
						Main.Snackbar.Appearance = Wpf.Ui.Common.ControlAppearance.Caution;
						Main.Snackbar.Show(VTOL.Resources.Languages.Language.ERROR, "The Profile Creation of" + SAVE_NAME__ + "Failed");
						wave_progress.Visibility = Visibility.Visible;
						Circe_progress.Visibility = Visibility.Hidden;
						Loading_Panel.Visibility = Visibility.Hidden;
						Options_Panel.Visibility = Visibility.Hidden;
						Add_Profile_Options_Panel.Visibility = Visibility.Hidden;
						Export_Profile_Options_Panel.Visibility = Visibility.Hidden;
					
					}
					finally
					{
						cancel = false;

						wave_progress.Visibility = Visibility.Visible;
						Circe_progress.Visibility = Visibility.Hidden;
						Loading_Panel.Visibility = Visibility.Hidden;
						Options_Panel.Visibility = Visibility.Hidden;
						Add_Profile_Options_Panel.Visibility = Visibility.Hidden;
						Export_Profile_Options_Panel.Visibility = Visibility.Hidden;
					}
				});
				}
			

			catch (Exception ex)
			{
				Log.Error(ex, $"A crash happened at {DateTime.Now.ToString("yyyy - MM - dd HH - mm - ss.ff", CultureInfo.InvariantCulture)}{Environment.NewLine}");
			}
		}
		
		public class Card_
		{
			public string Profile_Path_ { get; set; }
			public string Profile_Name_ { get; set; }
			public string Profile_Date_ { get; set; }
		}

		private async void LoadProfiles()
		{
			try
			{

				// Clear current listbox items
				Profile_List_Box.ItemsSource = null;
				Final_List.Clear();

				// Get all .vpb files in the directory
				string[] vpbFiles = await Task.Run(() => Directory.GetFiles(Main.User_Settings_Vars.NorthstarInstallLocation + "VTOL_profiles", "*.vbp", SearchOption.AllDirectories));
				if (vpbFiles != null)
				{
					
					foreach (string file in vpbFiles)
					{

						FileInfo fileInfo = new FileInfo(file);
						long fileSize = fileInfo.Length;
						DateTime creationDate = fileInfo.CreationTime;
						string formattedCreationDate = creationDate.ToString("MM/dd/yyyy HH:mm:ss");
						Final_List.Add(new Card_ { Profile_Name_ = fileInfo.Name, Profile_Date_ = formattedCreationDate, Profile_Path_ = fileInfo.FullName });

					}
					await Task.Delay(10);
					Profile_List_Box.ItemsSource = Final_List;

					//foreach (string file in Final_List)
					//{
					//	Profile_List_Box.Items.Add(file);

					//	Profile_List_Box.Refresh();
					//	await Task.Delay(50);
					//}
					// Hide loading icon
					//	LoadingIcon.Visibility = Visibility.Collapsed;
					// Output the list to console
					//vpbFiles.ToList().ForEach(//Console.WriteLine);

					_Completed_Mod_call = true;
				}
			}
			catch (Exception ex)
			{
				Log.Error(ex, $"A crash happened at {DateTime.Now.ToString("yyyy - MM - dd HH - mm - ss.ff", CultureInfo.InvariantCulture)}{Environment.NewLine}");
			}
		}
		private async void UnpackandCheck(string vtol_profiles_file_bin, string Target_Dir)
        {
			DispatchIfNecessary(async () =>
			{
				Options_Panel.Visibility = Visibility.Visible;
				Add_Profile_Options_Panel.Visibility = Visibility.Hidden;

			});
			//Console.WriteLine("Unpacking!");

				if (File.Exists(vtol_profiles_file_bin))
				{
					//Console.WriteLine("Found!");
					DispatchIfNecessary(async () =>
					{
						Current_File_Tag.Content = "Backing Up vtol_profiles_file_bin";
						wave_progress.Text = 0 + "%";
						wave_progress.Value = 0;
						Loading_Panel.Visibility = Visibility.Visible;

					});
					_cts = new CancellationTokenSource();
					var token = _cts.Token;
					try
					{
						// Display a message to the user indicating that the operation has started

						// Perform the long-running operation on a background thread
						
						var result = await Task.Run(() =>
						{
							return UnpackDirectory(vtol_profiles_file_bin, Target_Dir, token);
						});

						if (result == true)
						{
						DispatchIfNecessary(async () =>
						{
							//Console.WriteLine(result);
							//Console.WriteLine("Complete!");
							Main.Snackbar.Title = VTOL.Resources.Languages.Language.SUCCESS;
							Main.Snackbar.Appearance = Wpf.Ui.Common.ControlAppearance.Success;
							Main.Snackbar.Message = "Operation Complete - The Profile is now active";
							Main.Snackbar.Show();
							Loading_Panel.Visibility = Visibility.Hidden;
							Options_Panel.Visibility = Visibility.Hidden;
					});
					cancel = false;

						}
						else
						{
						DispatchIfNecessary(async () =>
						{
							Main.Snackbar.Title = VTOL.Resources.Languages.Language.ERROR;
							Main.Snackbar.Appearance = Wpf.Ui.Common.ControlAppearance.Caution;
							Main.Snackbar.Message = "Operation Failed / Cancelled";
							Main.Snackbar.Show();
							//Console.WriteLine(result);
							//Console.WriteLine("Failed!");
							wave_progress.Visibility = Visibility.Visible;
							Circe_progress.Visibility = Visibility.Hidden;
							Loading_Panel.Visibility = Visibility.Hidden;
							Options_Panel.Visibility = Visibility.Hidden;
							Add_Profile_Options_Panel.Visibility = Visibility.Hidden;
							Export_Profile_Options_Panel.Visibility = Visibility.Hidden;
					});
					cancel = false;


						}
					DispatchIfNecessary(async () =>
					{
						Loading_Panel.Visibility = Visibility.Hidden;
						Options_Panel.Visibility = Visibility.Hidden;
				});
				cancel = false;


					}



					catch (OperationCanceledException)
					{
					DispatchIfNecessary(async () =>
					{
						Main.Snackbar.Title = VTOL.Resources.Languages.Language.ERROR;
						Main.Snackbar.Appearance = Wpf.Ui.Common.ControlAppearance.Caution;
						Main.Snackbar.Message = "Operation Failed / Cancelled";
						Main.Snackbar.Show(); 
						wave_progress.Visibility = Visibility.Visible;
						Circe_progress.Visibility = Visibility.Hidden;
						Loading_Panel.Visibility = Visibility.Hidden;
						Options_Panel.Visibility = Visibility.Hidden;
						Add_Profile_Options_Panel.Visibility = Visibility.Hidden;
						Export_Profile_Options_Panel.Visibility = Visibility.Hidden;
				});
				cancel = false;

					}

					catch (Exception ex)
				{
					Log.Error(ex, $"A crash happened at {DateTime.Now.ToString("yyyy - MM - dd HH - mm - ss.ff", CultureInfo.InvariantCulture)}{Environment.NewLine}");

					DispatchIfNecessary(async () =>
						{
							Main.Snackbar.Title = "FATAL";
							Main.Snackbar.Appearance = Wpf.Ui.Common.ControlAppearance.Danger;
							Main.Snackbar.Message = "FAILED\n Could Not Find the file - " + vtol_profiles_file_bin;
							Main.Snackbar.Show();
							wave_progress.Visibility = Visibility.Visible;
							Circe_progress.Visibility = Visibility.Hidden;
							Loading_Panel.Visibility = Visibility.Hidden;
							Options_Panel.Visibility = Visibility.Hidden;
							Add_Profile_Options_Panel.Visibility = Visibility.Hidden;
							Export_Profile_Options_Panel.Visibility = Visibility.Hidden;
							cancel = false;
						});
					}
			}
				else
			{
				DispatchIfNecessary(async () =>
				{
					Main.Snackbar.Title = "FATAL";
					Main.Snackbar.Appearance = Wpf.Ui.Common.ControlAppearance.Danger;
					Main.Snackbar.Message = "FAILED\n Could Not Find the file - " + vtol_profiles_file_bin;
					Main.Snackbar.Show();
					wave_progress.Visibility = Visibility.Visible;
					Circe_progress.Visibility = Visibility.Hidden;
					Loading_Panel.Visibility = Visibility.Hidden;
					Options_Panel.Visibility = Visibility.Hidden;
					Add_Profile_Options_Panel.Visibility = Visibility.Hidden;
					Export_Profile_Options_Panel.Visibility = Visibility.Hidden;

					cancel = false;

				});

				}
			
		}

		public void CancelWork()
		{
			if (_cts != null)
			{
				_cts?.Cancel();
			}
		}
		public Page_Profiles()
		{


			InitializeComponent();


			//Load paths here
			if (!Directory.Exists(Main.User_Settings_Vars.NorthstarInstallLocation + "VTOL_profiles"))
			{

				Directory.CreateDirectory(Main.User_Settings_Vars.NorthstarInstallLocation + "VTOL_profiles");
			}
			SAVE_PATH__ = Main.User_Settings_Vars.NorthstarInstallLocation + "VTOL_profiles";
			Profile_Location.Text = SAVE_PATH__;
		}
		public static MainWindow GetMainWindow()
		{
			MainWindow mainWindow = null;

			foreach (Window window in Application.Current.Windows)
			{
				Type type = typeof(MainWindow);
				if (window != null && window.DependencyObjectType.Name == type.Name)
				{
					mainWindow = (MainWindow)window;
					if (mainWindow != null)
					{
						break;
					}
				}
			}


			return mainWindow;

		}
		private void ComboBox_DropDownClosed(object sender, EventArgs e)
		{
			Extra_Menu.Text = null;
		}
		public class DirectoryReader
		{
			public static DirectoryData ReadDirectory(string path)
			{
				using (var stream = File.Open(path, FileMode.Open))
				{
					var formatter = new BinaryFormatter();
					return (DirectoryData)formatter.Deserialize(stream);

				}
			}
		}
		public  void FadeControl(UIElement control, bool? show = null, double duration = 0.5)
		{
			DispatchIfNecessary(async () =>
			{
				// Determine the target visibility
				Visibility targetVisibility;
				if (show == true)
				{
					targetVisibility = Visibility.Visible;
				}
				else if (show == false)
				{
					targetVisibility = Visibility.Hidden;
				}
				else
				{
					targetVisibility = control.Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;
				}

				// Check if the control is already at the target visibility
				if (control.Visibility == targetVisibility)
					return;

				// Create a DoubleAnimation to animate the control's opacity
				DoubleAnimation da = new DoubleAnimation
				{
					From = control.Opacity,
					To = targetVisibility == Visibility.Visible ? 1 : 0,
					Duration = new Duration(TimeSpan.FromSeconds(duration)),
					AutoReverse = false
				};

				// Start the animation
				control.BeginAnimation(OpacityProperty, da);

				// Update the control's visibility
				control.Visibility = targetVisibility;

				// Wait for the animation to finish
				await Task.Delay((int)(duration * 1000));
			});
		}

		public void Export(string path, string[] includedFolders, string[] includedFiles) {
            try { 
			//Profile_Name.Text = SAVE_NAME__;
			//Profile_Location.Text = SAVE_PATH__;
			string NS_Mod_Dir = Main.User_Settings_Vars.NorthstarInstallLocation + @"R2Northstar\packages";
			if (!Directory.Exists(NS_Mod_Dir))
			{
				return;
			}
			System.IO.DirectoryInfo rootDirs = new DirectoryInfo(@NS_Mod_Dir);
			System.IO.DirectoryInfo[] subDirs = null;
			subDirs = rootDirs.GetDirectories();


			VERSION = Main.User_Settings_Vars.CurrentVersion;
			SAVE_NAME__ = Profile_Name.Text;
			NUMBER_MODS__ = subDirs.Length;
			SIZE = FileSizeCalculator.Pack(path, includedFolders, includedFiles);

			Options_Panel.Visibility = Visibility.Visible;
			Export_Profile_Options_Panel.Visibility = Visibility.Visible;
			NORTHSTAR_VERSION.Content = VERSION;
			NUMBER_OF_MODS.Content = NUMBER_MODS__;
			TOTAL_SIZE.Content = SIZE;
			Current_File_Tag.Content = "Processing the Directory - " + path;

			wave_progress.Visibility = Visibility.Hidden;
			Circe_progress.Visibility = Visibility.Visible;

			}

			catch (Exception ex)
			{
				Log.Error(ex, $"A crash happened at {DateTime.Now.ToString("yyyy - MM - dd HH - mm - ss.ff", CultureInfo.InvariantCulture)}{Environment.NewLine}");
			}
		}


		private void Export_Profile_Click(object sender, RoutedEventArgs e)
		{
			cancel = false;

			Export(Main.User_Settings_Vars.NorthstarInstallLocation, Folders, Files);
		}

		private void Import_Profile_Click(object sender, RoutedEventArgs e)
		{
			try
			{

				if (sender.GetType() == typeof(Wpf.Ui.Controls.Button))
				{
					cancel = false;


					Wpf.Ui.Controls.Button Button_ = (Wpf.Ui.Controls.Button)sender;
					string Name_ = Button_.Tag.ToString();
					if (Name_ != null)
					{
						if (File.Exists(Name_))
						{
							FileInfo f = new FileInfo(Name_);
							//FadeControl(Options_Panel, true, 2);
							DispatchIfNecessary(async () =>
							{
								Options_Panel.Visibility = Visibility.Visible;
							});
								if (Directory.Exists(Main.User_Settings_Vars.NorthstarInstallLocation + "VTOL_profiles") && !File.Exists(Main.User_Settings_Vars.NorthstarInstallLocation + "VTOL_profiles\\" + f.Name))
							{
								TryCopyFile(Name_, Main.User_Settings_Vars.NorthstarInstallLocation + "VTOL_profiles\\" + f.Name);



							}
							DispatchIfNecessary(async () =>
							{
								UnpackRead_BIN_INFO(Name_);


								LoadProfiles();

							});
							DispatchIfNecessary(async () =>
							{
								FadeControl(Add_Profile_Options_Panel, true, 1.5);

							});
						}
					}
				}






			}

			catch (Exception ex)
			{
				Log.Error(ex, $"A crash happened at {DateTime.Now.ToString("yyyy - MM - dd HH - mm - ss.ff", CultureInfo.InvariantCulture)}{Environment.NewLine}");


			}
		
		

		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{

		}

		private void Button_Click_1(object sender, RoutedEventArgs e)
		{
		}

		private void Export_Profile_BTN(object sender, RoutedEventArgs e)
        {
            try {
			Loading_Panel.Visibility = Visibility.Visible;
			Add_Profile_Options_Panel.Visibility = Visibility.Hidden;
			Export_Profile_Options_Panel.Visibility = Visibility.Hidden;
			Options_Panel.Visibility = Visibility.Visible;
			Main.Snackbar.Appearance = Wpf.Ui.Common.ControlAppearance.Info;
			Main.Snackbar.Show("INFO", "Packing Profile now");
			Pack_Label.Content = "Packing the File/Folder";

			Pack(Main.User_Settings_Vars.NorthstarInstallLocation, Folders, Files);
		}

	catch (Exception ex)
{
 Log.Error(ex, $"A crash happened at {DateTime.Now.ToString("yyyy - MM - dd HH - mm - ss.ff", CultureInfo.InvariantCulture)}{Environment.NewLine}");
	}

}

		private void Import_BTN(object sender, RoutedEventArgs e)
		{
			Pack_Label.Content = "UnPacking the File/Folder";
			cancel = false;

			UnpackandCheck(CURRENT_FILE__, Main.User_Settings_Vars.NorthstarInstallLocation);

		}

		private void I_Export_Mods_Checked(object sender, RoutedEventArgs e)
		{
			Skip_Mods = true;

		}

		private void I_Export_Mods_Unchecked(object sender, RoutedEventArgs e)
		{
			Skip_Mods = false;
		}

		private void Exit_BTN_Click(object sender, RoutedEventArgs e)
        {
            try { 
			CancelWork();
			Options_Panel.Visibility = Visibility.Hidden;
			Export_Profile_Options_Panel.Visibility = Visibility.Hidden;
			Loading_Panel.Visibility = Visibility.Hidden;
			Add_Profile_Options_Panel.Visibility = Visibility.Hidden;
			cancel = true;
			}

			catch (Exception ex)
			{
				Log.Error(ex, $"A crash happened at {DateTime.Now.ToString("yyyy - MM - dd HH - mm - ss.ff", CultureInfo.InvariantCulture)}{Environment.NewLine}");
			}
		}

		private void Add_Profile_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				string path = null;


				var dialog = new Ookii.Dialogs.Wpf.VistaOpenFileDialog();
				dialog.Filter = "vbp files (*.vbp)|*.vbp"; // Only show .vbp files
				dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer);

				string FileName_Last;
				var result = dialog.ShowDialog();
				if (result == true)
				{
					path = dialog.FileName;

				}
				if (File.Exists(path))
				{
					FileInfo f = new FileInfo(path);
					FadeControl(Options_Panel, true, 2);
                    if (Directory.Exists(Main.User_Settings_Vars.NorthstarInstallLocation + "VTOL_profiles"))
                    {
						TryCopyFile(path, Main.User_Settings_Vars.NorthstarInstallLocation + "VTOL_profiles\\"+f.Name);



					}

					UnpackRead_BIN_INFO(path);

					DispatchIfNecessary(async () =>
					{

						LoadProfiles();

					});
					DispatchIfNecessary(async () =>
					{
						FadeControl(Add_Profile_Options_Panel, true, 1.5);

					});
				}




			}

			catch (Exception ex)
			{
				Log.Error(ex, $"A crash happened at {DateTime.Now.ToString("yyyy - MM - dd HH - mm - ss.ff", CultureInfo.InvariantCulture)}{Environment.NewLine}");
			}
		}

		private void Extra_Menu_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
		
}

		private void save_Lcoation_Btn_Click(object sender, RoutedEventArgs e)
		{

			var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
			//dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer);
			string path = null;

			var result = dialog.ShowDialog();
			if (result == true)
			{
				path = dialog.SelectedPath;


			}
			if (Directory.Exists(path))
			{
				SAVE_PATH__ = path;
				Profile_Location.Text = SAVE_PATH__;
				Main.Snackbar.Appearance = Wpf.Ui.Common.ControlAppearance.Success;
				Main.Snackbar.Show("SUCCESS!", "The Path has been set to - " + path);
			}
		}

		private void Exit_BTN_ADD_Prfl_Click(object sender, RoutedEventArgs e)
		{
			CancelWork();
			Options_Panel.Visibility = Visibility.Hidden;
			Export_Profile_Options_Panel.Visibility = Visibility.Hidden;
			Loading_Panel.Visibility = Visibility.Hidden;
			Add_Profile_Options_Panel.Visibility = Visibility.Hidden;
			cancel = true;

		}

		private void Export_Mods_Checked(object sender, RoutedEventArgs e)
		{
			Do_Not_save_Mods = true;

		}

		private void Export_Mods_Unchecked(object sender, RoutedEventArgs e)
		{
			Do_Not_save_Mods = false;

		}

		private void Profile_Name_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (Profile_Name.Text != "Default Profile")
			{

				Profile_Name.Foreground = Brushes.White;
			}
			SAVE_NAME__ = Profile_Name.Text;
		}

		private void Exit_BTN_ADD_Prfl_Click_1(object sender, RoutedEventArgs e)
        {
            try { 
			CancelWork();
			Options_Panel.Visibility = Visibility.Hidden;
			Export_Profile_Options_Panel.Visibility = Visibility.Hidden;
			Loading_Panel.Visibility = Visibility.Hidden;
			Add_Profile_Options_Panel.Visibility = Visibility.Hidden;
				cancel = true;

			}

			catch (Exception ex)
		{
    Log.Error(ex, $"A crash happened at {DateTime.Now.ToString("yyyy - MM - dd HH - mm - ss.ff", CultureInfo.InvariantCulture)}{Environment.NewLine}");
			}

		}

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
		//	_cts.Dispose();

		}

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            try { 
			DispatchIfNecessary(async () =>
			{
				if (Main.Profile_TAG.Content == null || Main.Profile_TAG.Content == "" || Main.Profile_TAG.Content.ToString().Length < 1)
				{
					Clear_Profile.IsEnabled = false;

				}else if (Properties.Settings.Default.Profile_Name == "" || Properties.Settings.Default.Profile_Name == null || Properties.Settings.Default.Profile_Name.Length < 1)
                {
					Clear_Profile.IsEnabled = false;

				}
				else
				{
					Clear_Profile.IsEnabled = true;


				}
				LoadProfiles();
				//PopulateListBoxWithRandomPaths();
			});

		}

	catch (Exception ex)
{
 Log.Error(ex, $"A crash happened at {DateTime.Now.ToString("yyyy - MM - dd HH - mm - ss.ff", CultureInfo.InvariantCulture)}{Environment.NewLine}");
	}
}
		

		
		private void Profile_List_Box_Loaded(object sender, RoutedEventArgs e)
        {
			
			
		}

        private void Card___MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
			try
			{
				string Triggger = null;

				if (_Completed_Mod_call == true)
				{
					Wpf.Ui.Controls.CardControl Card;
					if (sender.GetType() == typeof(Wpf.Ui.Controls.CardControl))
					{
						Card = sender as Wpf.Ui.Controls.CardControl;

						DockPanel DockPanel_ = FindVisualChild<DockPanel>(Card);
						Triggger = DockPanel_.Tag.ToString();

						if (Triggger != null)
						{
							if (Triggger == "Hidden")
							{




								DoubleAnimation da = new DoubleAnimation
								{
									From = DockPanel_.Opacity,
									To = 1,
									Duration = new Duration(TimeSpan.FromSeconds(0.4)),
									AutoReverse = false
								};
								DockPanel_.BeginAnimation(OpacityProperty, da);
								DockPanel_.IsEnabled = true;
								Triggger = "Visible";
								DockPanel_.Tag = "Visible";
								DockPanel_.Visibility = Visibility.Visible;




							}
							else if (Triggger == "Visible")
							{


								DoubleAnimation da = new DoubleAnimation
								{
									From = DockPanel_.Opacity,
									To = 0,
									Duration = new Duration(TimeSpan.FromSeconds(0.4)),
									AutoReverse = false
								};
								DockPanel_.BeginAnimation(OpacityProperty, da);
								DockPanel_.IsEnabled = false;
								Triggger = "Hidden";
								DockPanel_.Tag = "Hidden";






							}
						}


					}




				}
			}
			catch (Exception ex)
			{
				Log.Error(ex, $"A crash happened at {DateTime.Now.ToString("yyyy - MM - dd HH - mm - ss.ff", CultureInfo.InvariantCulture)}{Environment.NewLine}");

			}

		}

        private void CardControl_IsMouseCaptureWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
        {

        }

        private void CardControl_GotFocus(object sender, RoutedEventArgs e)
        {

        }
		private childItem FindVisualChild<childItem>(DependencyObject obj)
  where childItem : DependencyObject
		{
			try
			{
				for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
				{
					DependencyObject child = VisualTreeHelper.GetChild(obj, i);
					if (child != null && child is childItem)
					{
						return (childItem)child;
					}
					else
					{
						childItem childOfChild = FindVisualChild<childItem>(child);
						if (childOfChild != null)
							return childOfChild;
					}
				}
				return null;

			}
			catch (Exception ex)
			{

			}
			return null;
		}
		private void Card___Loaded(object sender, RoutedEventArgs e)
        {
			try
			{
				if (_Completed_Mod_call == true)
				{
					Wpf.Ui.Controls.CardControl Card;
					if (sender.GetType() == typeof(Wpf.Ui.Controls.CardControl))
					{
						Card = sender as Wpf.Ui.Controls.CardControl;

						DockPanel DockPanel_ = FindVisualChild<DockPanel>(Card);

						DockPanel_.Visibility = Visibility.Hidden;

						DockPanel_.Tag = "Hidden";
						DockPanel_.IsEnabled = false;
						DockPanel_.Opacity = 0.0;

					}
				}
			}
			catch (Exception ex)
			{
				Log.Error(ex, $"A crash happened at {DateTime.Now.ToString("yyyy - MM - dd HH - mm - ss.ff", CultureInfo.InvariantCulture)}{Environment.NewLine}");

			}
		}

        private void CardControl_MouseMove(object sender, MouseEventArgs e)
        {

        }

        private void Delete_Btn_Click(object sender, RoutedEventArgs e)
        {
			try
			{

				if (sender.GetType() == typeof(Wpf.Ui.Controls.Button))
				{

					Wpf.Ui.Controls.Button Button_ = (Wpf.Ui.Controls.Button)sender;
					string Name_ = Button_.Tag.ToString();
					if (Name_ != null)
					{


						TryDeleteFile(Name_);
						DispatchIfNecessary(async () =>
						{
							LoadProfiles();
							//PopulateListBoxWithRandomPaths();
						});
					}
				}






			}

			catch (Exception ex)
			{
				Log.Error(ex, $"A crash happened at {DateTime.Now.ToString("yyyy - MM - dd HH - mm - ss.ff", CultureInfo.InvariantCulture)}{Environment.NewLine}");
				

			}
		}

        private void I_Backup_Mods_Unchecked(object sender, RoutedEventArgs e)
        {
			Backup_Profile_Current = false;

		}

        private void I_Backup_Mods_Checked(object sender, RoutedEventArgs e)
        {
			Backup_Profile_Current = true;

		}

        private void Clear_Profile_Click(object sender, RoutedEventArgs e)
        {
            try {
				DispatchIfNecessary(async () =>
				{
					Main.Profile_TAG.Content = "";
					Properties.Settings.Default.Profile_Name = "";
					Properties.Settings.Default.Save();
					Main.Profile_TAG.Content = Properties.Settings.Default.Profile_Name;
					Main.Profile_TAG.Refresh();
				});
		}

			catch (Exception ex)
			{
				Log.Error(ex, $"A crash happened at {DateTime.Now.ToString("yyyy - MM - dd HH - mm - ss.ff", CultureInfo.InvariantCulture)}{Environment.NewLine}");
				

			}
}

        private void Clear_Profile_Initialized(object sender, EventArgs e)
        {
			
		}

        private void Clear_Profile_Loaded(object sender, RoutedEventArgs e)
        {
			
		}
    }
		}
