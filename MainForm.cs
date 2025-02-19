using System;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Drawing.Imaging;
using System.Linq;

namespace PictureSorter
{
    public partial class MainForm : Form
    {
        private TextBox txtSourcePath;
        private TextBox txtDestPath;
        private Button btnSelectSource;
        private Button btnSelectDest;
        private Button btnStart;
        private ProgressBar progressBar;
        private Label lblStatus;

        public MainForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "照片自动分类工具";
            this.Size = new Size(600, 300);
            this.StartPosition = FormStartPosition.CenterScreen;

            // 创建控件
            txtSourcePath = new TextBox
            {
                Location = new Point(20, 20),
                Size = new Size(400, 23),
                ReadOnly = true
            };

            btnSelectSource = new Button
            {
                Location = new Point(440, 20),
                Size = new Size(120, 23),
                Text = "选择源文件夹"
            };
            btnSelectSource.Click += BtnSelectSource_Click;

            txtDestPath = new TextBox
            {
                Location = new Point(20, 60),
                Size = new Size(400, 23),
                ReadOnly = true
            };

            btnSelectDest = new Button
            {
                Location = new Point(440, 60),
                Size = new Size(120, 23),
                Text = "选择目标文件夹"
            };
            btnSelectDest.Click += BtnSelectDest_Click;

            btnStart = new Button
            {
                Location = new Point(20, 100),
                Size = new Size(540, 30),
                Text = "开始处理"
            };
            btnStart.Click += BtnStart_Click;

            progressBar = new ProgressBar
            {
                Location = new Point(20, 150),
                Size = new Size(540, 23)
            };

            lblStatus = new Label
            {
                Location = new Point(20, 190),
                Size = new Size(540, 23),
                Text = "就绪"
            };

            // 添加控件到窗体
            this.Controls.AddRange(new Control[]
            {
                txtSourcePath,
                btnSelectSource,
                txtDestPath,
                btnSelectDest,
                btnStart,
                progressBar,
                lblStatus
            });
        }

        private void BtnSelectSource_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtSourcePath.Text = dialog.SelectedPath;
                }
            }
        }

        private void BtnSelectDest_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtDestPath.Text = dialog.SelectedPath;
                }
            }
        }

        private async void BtnStart_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtSourcePath.Text) || string.IsNullOrEmpty(txtDestPath.Text))
            {
                MessageBox.Show("请选择源文件夹和目标文件夹", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnStart.Enabled = false;
            btnSelectSource.Enabled = false;
            btnSelectDest.Enabled = false;

            try
            {
                string[] imageFiles = Directory.GetFiles(txtSourcePath.Text, "*.*")
                    .Where(file =>
                    {
                        string ext = Path.GetExtension(file).ToLower();
                        return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".gif";
                    }).ToArray();

                if (imageFiles.Length == 0)
                {
                    MessageBox.Show("源文件夹中没有找到图片文件", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                progressBar.Maximum = imageFiles.Length;
                progressBar.Value = 0;
                int processedCount = 0;

                var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
                await Task.Run(() => Parallel.ForEach(imageFiles, options, file =>
                {
                    try
                    {
                        DateTime? dateTaken = null;
                        using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (var img = Image.FromStream(stream))
                        {
                            try
                            {
                                PropertyItem propItem = img.PropertyItems.FirstOrDefault(p => p.Id == 0x9003);
                                if (propItem != null)
                                {
                                    string dateTakenStr = System.Text.Encoding.ASCII.GetString(propItem.Value).Trim();
                                    if (DateTime.TryParseExact(dateTakenStr.Substring(0, 10), "yyyy:MM:dd",
                                        System.Globalization.CultureInfo.InvariantCulture,
                                        System.Globalization.DateTimeStyles.None, out DateTime dt))
                                    {
                                        dateTaken = dt;
                                    }
                                }
                            }
                            catch
                            {
                                // 如果读取EXIF信息失败，使用文件创建时间
                                dateTaken = File.GetCreationTime(file);
                            }
                        }

                        if (!dateTaken.HasValue)
                        {
                            dateTaken = File.GetCreationTime(file);
                        }

                        string yearMonth = dateTaken.Value.ToString("yyyy-MM");
                        string destFolder = Path.Combine(txtDestPath.Text, yearMonth);

                        lock (this)
                        {
                            if (!Directory.Exists(destFolder))
                            {
                                Directory.CreateDirectory(destFolder);
                            }
                        }

                        string destFile = Path.Combine(destFolder, Path.GetFileName(file));
                        File.Move(file, destFile, true);

                        int currentCount = Interlocked.Increment(ref processedCount);
                        this.Invoke((MethodInvoker)delegate
                        {
                            progressBar.Value = currentCount;
                            lblStatus.Text = $"正在处理: {currentCount}/{progressBar.Maximum}";
                        });
                    }
                    catch (Exception ex)
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            lblStatus.Text = $"处理文件 {Path.GetFileName(file)} 时出错: {ex.Message}";
                        });
                    }
                }));

                MessageBox.Show("处理完成！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                lblStatus.Text = "就绪";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"处理过程中出错：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnStart.Enabled = true;
                btnSelectSource.Enabled = true;
                btnSelectDest.Enabled = true;
            }
        }
    }
}