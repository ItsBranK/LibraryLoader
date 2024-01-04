﻿using System;
using System.Diagnostics;
using System.Windows.Forms;
using System.Collections.Generic;
using System.IO;
using LibraryLoader.Framework;
using System.Configuration;
using LibraryLoader.Forms;

namespace LibraryLoader
{
    public enum StatusTypes : byte
    {
        NoFile,
        NoProcess,
        FoundProcess,
        DelayingLoad,
        LoadSuccess,
        LoadFailure
    }

    public partial class MainFrm : Form
    {
        private static StatusTypes _status = StatusTypes.NoProcess;
        private static bool _autoLoad = false;
        private static string _libraryFile = "";
        private static string _processName = "";
        private static Int32 _processId = 0;

        public MainFrm()
        {
            InitializeComponent();
        }

        private void MainFrm_Load(object sender, EventArgs e)
        {
            this.Text = Assembly.GetTitle();
            LoadProcesses();
            SetStatus(StatusTypes.NoFile);
            ProcessTmr.Start();
        }

        private void AboutMenuItem_Click(object sender, EventArgs e)
        {
            AboutFrm aboutFrm = new AboutFrm();
            aboutFrm.Show();
        }

        private void ProcessBx_SelectedValueChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(ProcessBx.Text))
            {
                Int32 pid = (ProcessBx.Text.IndexOf('[') + 1);

                if (pid != -1)
                {
                    string pidStr = ProcessBx.Text.Substring(pid, ((ProcessBx.Text.Length - pid) - 1));

                    if (Int32.TryParse(pidStr, out pid) && (pid > 0))
                    {
                        Process? foundProcess = FProcess.GetProcess(pid);

                        if (foundProcess != null)
                        {
                            SelectProcess(foundProcess);
                        }
                        else
                        {
                            LoadProcesses();
                        }
                    }
                }
            }
        }

        private void PIDBx_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                Process? foundProcess = FProcess.GetProcess((Int32)PIDBx.Value);

                if (foundProcess != null)
                {
                    SelectProcess(foundProcess);
                }
                else
                {
                    LoadProcesses();
                }
            }
        }

        private void RefreshBtn_Click(object sender, EventArgs e)
        {
            LoadProcesses();
        }

        private void BrowseBtn_Click(object sender, EventArgs e)
        {
            OpenFileDialog OpenFileDialog = new OpenFileDialog
            {
                Title = "Select a dll to add",
                Filter = "DLL Files (*.dll)|*.dll"
            };

            if (OpenFileDialog.ShowDialog() == DialogResult.OK)
            {
                FileInfo fileInfo = new FileInfo(OpenFileDialog.FileName);

                if (File.Exists(fileInfo.FullName))
                {
                    FileBx.Enabled = true;
                    FileBx.Text = fileInfo.FullName;
                    FileBx.Enabled = false;
                    _libraryFile = FileBx.Text;
                    SetStatus(StatusTypes.NoProcess);
                }
                else
                {
                    SetStatus(StatusTypes.NoFile);
                }
            }
        }

        private void DelayBx_ValueChanged(object sender, EventArgs e)
        {
            DelayTmr.Interval = (Int32)DelayBx.Value;
        }

        private void AutoBx_CheckedChanged(object sender, EventArgs e)
        {
            ProcessTmr.Stop();
            _autoLoad = AutoBx.Checked;
            ProcessTmr.Start();
        }

        private void LoadBtn_Click(object sender, EventArgs e)
        {
            if (ShouldInject())
            {
                InjectFile();
            }
        }

        private void ProcessTmr_Tick(object sender, EventArgs e)
        {
            Process? process = FindSelectedProcess();

            if (ShouldInject() && (process != null))
            {
                LoadBtn.Enabled = true;
                AutoBx.Enabled = true;

                if (_autoLoad)
                {
                    if ((_status != StatusTypes.DelayingLoad) && !string.IsNullOrEmpty(_processName))
                    {
                        SetStatus(StatusTypes.DelayingLoad);
                        DelayTmr.Start();
                    }
                }
                else
                {
                    DelayTmr.Stop();
                    SetStatus(StatusTypes.FoundProcess);
                }
            }
            else if (process == null)
            {
                DelayTmr.Stop();

                if (string.IsNullOrEmpty(_libraryFile))
                {
                    SetStatus(StatusTypes.NoFile);
                }
                else
                {
                    SetStatus(StatusTypes.NoProcess);
                }
            }
        }

        private void DelayTmr_Tick(object sender, EventArgs e)
        {
            if (ShouldInject() && (_autoLoad && !string.IsNullOrEmpty(_processName)))
            {
                InjectFile();
            }

            DelayTmr.Stop();
        }

        private void SetStatus(StatusTypes status)
        {
            _status = status;

            switch (_status)
            {
                case StatusTypes.NoFile:
                    StatusLbl.Text = "Waiting for user to select a file.";
                    break;
                case StatusTypes.NoProcess:
                    StatusLbl.Text = "Waiting for process to start...";
                    break;
                case StatusTypes.FoundProcess:
                    StatusLbl.Text = "Process found, ready to load file!";
                    break;
                case StatusTypes.DelayingLoad:
                    StatusLbl.Text = "Process found, auto loading...";
                    break;
                case StatusTypes.LoadSuccess:
                    StatusLbl.Text = "Successfully loaded the file!";
                    break;
                case StatusTypes.LoadFailure:
                    StatusLbl.Text = "Failed to loaded the file!";
                    break;
            }

            if ((_status == StatusTypes.NoFile) || (_status == StatusTypes.NoProcess))
            {
                this.Text = Assembly.GetTitle();
                FLoader.ClearHandleCache();
                LoadBtn.Enabled = false;
                AutoBx.Enabled = true;
            }
        }

        private Process? FindSelectedProcess()
        {
            if (_autoLoad && !string.IsNullOrEmpty(_processName))
            {
                return FProcess.GetProcess(_processName);
            }
            else if (_processId != 0)
            {
                return FProcess.GetProcess(_processId);
            }

            return null;
        }

        private void LoadProcesses()
        {
            AutoBx.Checked = false; // Don't want to inject into a random process, if the user accidently selected the wrong one at first (I've done this a few times).
            _processName = "";
            ProcessBx.Items.Clear();
            PIDBx.Value = 0;
            _processId = 0;
            SetStatus(StatusTypes.NoProcess);

            Process[] processList = Process.GetProcesses();
            List<string> processNames = new List<string>();

            foreach (Process process in processList)
            {
                if (process != null)
                {
                    processNames.Add(process.ProcessName + " [" + process.Id.ToString() + "]");
                }
            }

            processNames.Sort();

            foreach (string processName in processNames)
            {
                ProcessBx.Items.Add(processName);
            }
        }

        private void SelectProcess(Process? process)
        {
            if (process != null)
            {
                string processEntry = (process.ProcessName + " [" + process.Id.ToString() + "]");

                if (!ProcessBx.Items.Contains(processEntry))
                {
                    LoadProcesses();
                }

                if (!string.IsNullOrEmpty(process.MainWindowTitle))
                {
                    _processName = process.MainWindowTitle;
                }
                else
                {
                    _processName = process.ProcessName;
                }

                ProcessBx.Text = processEntry;
                PIDBx.Value = process.Id;

                if (_processId != process.Id)
                {
                    SetStatus(StatusTypes.NoProcess);
                }

                _processId = process.Id;
            }
        }

        private bool ShouldInject()
        {
            if (!string.IsNullOrEmpty(_libraryFile) && (_status != StatusTypes.LoadSuccess) && (_status != StatusTypes.LoadFailure))
            {
                return ((_autoLoad && !string.IsNullOrEmpty(_processName)) || (_processId != 0));
            }

            return false;
        }

        private void InjectFile()
        {
            if (ShouldInject())
            {
                if (File.Exists(_libraryFile))
                {
                    Process? process = FindSelectedProcess();

                    if (process != null)
                    {
                        InjectionResults result = FLoader.LoadLibrary(process, _libraryFile);

                        if ((result == InjectionResults.Success) || (result == InjectionResults.AlreadyInjected))
                        {
                            this.Text = (Assembly.GetTitle() + " - " + _processName);
                            SetStatus(StatusTypes.LoadSuccess);
                            LoadBtn.Enabled = false;
                            AutoBx.Enabled = false;
                        }
                        else
                        {
                            SetStatus(StatusTypes.LoadFailure);
                            LoadBtn.Enabled = true;
                            AutoBx.Enabled = true;
                        }
                    }
                    else
                    {
                        SetStatus(StatusTypes.NoProcess);
                        LoadBtn.Enabled = false;
                        AutoBx.Enabled = true;
                    }
                }
                else
                {
                    SetStatus(StatusTypes.NoFile);
                    _libraryFile = "";
                    LoadBtn.Enabled = false;
                    AutoBx.Enabled = true;
                }
            }
        }
    }
}