﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Codaxy.WkHtmlToPdf;
using MetroFramework.Forms;
using System.IO;
using System.Reflection;
using System.Xml;

namespace cnblogbackup
{
    public partial class MainForm : MetroForm
    {
        private List<Task> task_set = null;
        public delegate void UpdateProgress(string _link_url, string _title, bool is_success);
        public UpdateProgress my_delegate;
        public delegate void UpdateMax(int input);
        public UpdateMax max_delegate;
        public delegate void LogAppendDelegate(Color color, string text);
        private XmlDocument root_xml_doc = null;
        private int success_count = 0;
        private int wrong_count = 0;

        private void UpdateProgressMethod(string _link_url, string _title, bool is_success = true)
        {
            if (is_success)
            {
                this.ProgressBar.Value += 1;
                success_count++;
                LogMessage("完成对《" + _title + "》的保存"
                    + Environment.NewLine
                    + "文章链接:" + _link_url);
            }
            else
            {
                this.ProgressBar.Value += 1;
                wrong_count++;
                LogError("失败！ " + _title + "保存未成功！"
                    + Environment.NewLine
                    + "文章链接:" + _link_url);
            }
            if (ProgressBar.Value == ProgressBar.Maximum)
            {
                ProgressBar.Style = MetroFramework.MetroColorStyle.Red;
                root_xml_doc.Save("../../lib/Configure.xml");
                LogMessage("已经完成全部备份！");
                LogMessage("本次共爬取保存" + success_count + "份博客,有"+wrong_count+"份博客爬取失败!");
            }
        }

        private void UpdateMaxMethod(int input)
        {
            ProgressBar.Maximum = input;
        }

        public MainForm()
        {
            InitializeComponent();
            my_delegate = new UpdateProgress(UpdateProgressMethod);
            max_delegate = new UpdateMax(UpdateMaxMethod);
        }

        private async void StartButton_Click(object sender, EventArgs e)
        {
            ProgressBar.Style = MetroFramework.MetroColorStyle.Teal;
            ProgressBar.Value = 0;
            success_count = 0;
            wrong_count = 0;
            LogText.Text = "开始备份...\n";
            //tasks = DiffLinkAndPreTask();
            await Task.Factory.StartNew(() => Backup(this));
        }

        private void Backup(MainForm _main_form_control)
        {
            List<TaskInfo> tasks_info = DiffLinkAndPreTask();
            // ProgressBar.Maximum = tasks.Count;
            if (tasks_info.Count != 0)
            {
                _main_form_control.Invoke(max_delegate, tasks_info.Count);
                for (int i = 0; i < tasks_info.Count; i++)
                {
                    try
                    {
                        TaskInfo info = tasks_info[i];
                        //task_set[i].ContinueWith((task) => TaskEndedFault(this, info.link_url, info.title), TaskContinuationOptions.OnlyOnFaulted);
                        task_set[i].ContinueWith((task) => TaskEnded(this, info.user, info.xml_doc, info.title, info.link_url), TaskContinuationOptions.NotOnFaulted);
                        task_set[i].RunSynchronously();
                        task_set[i].Wait();
                    }
                    catch (AggregateException e)
                    {
                        // if faulted,then do the false one's continue task.
                        this.Invoke(this.my_delegate, new object[] { tasks_info[i].link_url, tasks_info[i].title, false });
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.ToString());
                    }
                }
            }
            else
            {
                MessageBox.Show("没有新的博客需要备份！");
            }
        }

        private XmlNode GetLinkNode(XmlDocument root_node, string title, string url)
        {
            XmlNode temp = root_node.CreateElement("link");
            ((XmlElement)temp).SetAttribute("title", title);
            temp.InnerText = url;
            return temp;
        }

        private void ConfigureButton_Click(object sender, EventArgs e)
        {
            ConfigureForm config = new ConfigureForm();
            config.ShowDialog();
        }

        private List<TaskInfo> DiffLinkAndPreTask()
        {
            task_set = new List<Task>();
            List<TaskInfo> tasks_info = new List<TaskInfo>();
            XmlDocument xml_doc = new XmlDocument();
            root_xml_doc = xml_doc;
            try
            {
                xml_doc.Load("../../lib/Configure.xml");
                string root_path = ConfigureForm.ReadXmlToPath();
                Dictionary<string, XmlNode> blogs = ConfigureForm.ReadXmlToBlogNode(xml_doc);
                foreach (string user_number in blogs.Keys)
                {
                    XmlNode user = blogs[user_number];
                    string store_path = root_path + "/" + user_number;
                    if (!Directory.Exists(store_path))
                        Directory.CreateDirectory(store_path);
                    Dictionary<string, string> now_link = BlogsParser.UrlTitleDic(blogs[user_number].Attributes["home"].Value);
                    Dictionary<string, string> backup_link = ConfigureForm.ReadXmlToLink(blogs[user_number].Attributes["home"].Value);
                    foreach (string link_url in now_link.Keys)
                    {
                        // if the url has been backuped.
                        if (!backup_link.ContainsKey(link_url))
                        {
                            //add the task into the task_set
                            Task temp = PdfConvert.ConvertHtmlToPdfAsync(
                                new PdfDocument
                                {
                                    Url = link_url,
                                    HeaderLeft = link_url,
                                    HeaderRight = "[date] [time]",
                                    FooterCenter = "Page [page] of [topage]"

                                }, new PdfOutput
                                {
                                    // [title] +".pdf"
                                    OutputFilePath = store_path + "/" + now_link[link_url] + ".pdf"
                                }, this);
                            //temp_task.Start();
                            tasks_info.Add(
                                new TaskInfo
                                {
                                    user = user,
                                    xml_doc = xml_doc,
                                    link_url = link_url,
                                    title = now_link[link_url]
                                });
                            task_set.Add(temp);
                        }
                    }
                }
            }
            catch (System.IO.FileNotFoundException)
            {
                XmlNode node = xml_doc.CreateXmlDeclaration("1.0", "utf-8", "");
                xml_doc.AppendChild(node);
                XmlNode root_node = xml_doc.CreateElement("students");
                xml_doc.AppendChild(root_node);
                xml_doc.Save("../../lib/Configure.xml");
            }
            return tasks_info;
        }

        //log the error message
        public void LogError(string text)
        {
            LogAppendDelegate la = new LogAppendDelegate(LogAppend);
            LogText.Invoke(la, Color.Red, DateTime.Now.ToString("HH:mm:ss ") + text);
        }

        //log the correct message
        public void LogMessage(string text)
        {
            LogAppendDelegate la = new LogAppendDelegate(LogAppend);
            LogText.Invoke(la, Color.Black, DateTime.Now.ToString("HH:mm:ss ") + text);
        }

        public void LogAppend(Color color, string text)
        {
            LogText.AppendText(Environment.NewLine);
            LogText.SelectionColor = color;
            LogText.AppendText(text);
        }

        private void TaskEnded(MainForm _main_form_control, XmlNode _user, XmlDocument _xml_doc, string _title, string _link_url)
        {
            _user.AppendChild(GetLinkNode(_xml_doc, _title, _link_url));
            //Update progress and log's text
            _main_form_control.Invoke(_main_form_control.my_delegate, new object[] { _link_url, _title, true });
        }

        private void LogText_TextChanged(object sender, EventArgs e)
        {
            //Focus
            this.LogText.Focus();
            //Select the last.
            this.LogText.Select(this.LogText.TextLength, 0);
            this.LogText.ScrollToCaret();
        }
    }

    class TaskInfo
    {
        public XmlNode user { get; set; }
        public XmlDocument xml_doc { get; set; }
        public string link_url { get; set; }
        public string title { get; set; }
    }
}
