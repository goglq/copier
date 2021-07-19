using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Copier
{
    public partial class MainForm : Form
    {
        private Mutex _mutex = new Mutex();

        private CountdownEvent _countdownEvent = new CountdownEvent(4);

        private IList<string> _paths = new List<string>();

        private IList<ProgressBar> _progressBars = new List<ProgressBar>();

        private string _destination = null;

        private bool _isDone = false;

        public MainForm()
        {
            InitializeComponent();

            _progressBars.Add(progressBarFile1);
            _progressBars.Add(progressBarFile2);
            _progressBars.Add(progressBarFile3);
            _progressBars.Add(progressBarFile4);
        }

        private void btnChooseFiles_Click(object sender, EventArgs e)
        {
            ResetCopy();

            try
            {
                _paths.Clear();

                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Title = "Choose files to copy";
                openFileDialog.Multiselect = true;

                if (openFileDialog.ShowDialog() != DialogResult.OK)
                    return;
                if (openFileDialog.FileNames.Length != 4)
                    throw new ArgumentException();

                openFileDialog.FileNames.ToList().ForEach(fileName => _paths.Add(fileName));
            }
            catch (ArgumentException)
            {
                MessageBox.Show("You have to choose 4 files", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }



        private void btnChooseTargetDir_Click(object sender, EventArgs e)
        {
            CommonOpenFileDialog commonOpenFileDialog = new CommonOpenFileDialog();
            commonOpenFileDialog.IsFolderPicker = true;
            commonOpenFileDialog.Multiselect = false;

            if (commonOpenFileDialog.ShowDialog() != CommonFileDialogResult.Ok) return;

            _destination = commonOpenFileDialog.FileName;
        }

        private void btnCopy_Click(object sender, EventArgs e)
        {
            new Thread(StartCopying).Start();
        }

        private void StartCopying()
        {
            try
            {
                if (_paths.Count != 4)
                    throw new Exception("Select source files");
                if (_destination is null)
                    throw new Exception("Select Destination");

                for (int i = 0; i < 4; i++)
                {
                    int progressBarIndex = i;
                    string path = _paths[progressBarIndex];
                    new Thread(() =>
                    {
                        CopyFiles(path, _destination, progressBarIndex);
                    }).Start();
                }
                _countdownEvent.Wait();
                if (_isDone)
                    MessageBox.Show("All files copied.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void CopyFiles(string source, string target, int progressBarIndex)
        {
            ProgressBar progressBar = _progressBars[progressBarIndex];
            FileInfo fileInfo = new FileInfo(source);

            PrepareProgressBar(progressBar, fileInfo);
            CopyFile(source, target, progressBar, fileInfo);
            _isDone = true;
            _countdownEvent.Signal();
        }

        private void CopyFile(string source, string target, ProgressBar progressBar, FileInfo fileInfo)
        {
            byte[] buffer = new byte[2048];

            using (Stream targetStream = File.Create(Path.Combine(target, fileInfo.Name)))
            {
                using (Stream sourceStream = File.OpenRead(source))
                {
                    CopyChunk(progressBar, buffer, targetStream, sourceStream);
                }
            }
        }

        private void CopyChunk(ProgressBar progressBar, byte[] buffer, Stream targetStream, Stream sourceStream)
        {
            while (sourceStream.Position < sourceStream.Length)
            {
                int readBytes = sourceStream.Read(buffer, 0, buffer.Length);
                targetStream.Write(buffer, 0, readBytes);

                progressBar.Invoke(new MethodInvoker(delegate
                {
                    progressBar.Value += readBytes;
                }));

                _mutex.WaitOne();
                progressBarTotal.Invoke(new MethodInvoker(delegate
                {
                    progressBarTotal.Value += readBytes;
                }));
                _mutex.ReleaseMutex();
            }
        }

        private void PrepareProgressBar(ProgressBar progressBar, FileInfo fileInfo)
        {
            progressBar.Invoke(new MethodInvoker(delegate
            {
                progressBar.Maximum = (int)fileInfo.Length;
            }));

            _mutex.WaitOne();
            progressBar.Invoke(new MethodInvoker(delegate
            {
                progressBarTotal.Maximum += (int)fileInfo.Length;
            }));
            _mutex.ReleaseMutex();
        }

        private void ResetCopy()
        {
            ResetProgressBars();
            _countdownEvent.Reset();
            _isDone = false;
        }

        private void ResetProgressBars()
        {
            foreach (ProgressBar progressBar in _progressBars)
            {
                progressBar.Invoke(new MethodInvoker(delegate
                {
                    progressBar.Value = 0;
                }));
            }

            progressBarTotal.Invoke(new MethodInvoker(delegate
            {
                progressBarTotal.Value = 0;
            }));
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _countdownEvent.Dispose();
            _mutex.Close();
        }
    }
}
