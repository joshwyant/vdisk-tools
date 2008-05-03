using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using OSDevelopment.DiskImages;

namespace vgui
{
    public partial class Form1 : Form
    {
        FATVolume vol;
        string tempFol;
        Stack<string> Forward;
        Stack<string> Back;
        public Form1()
        {
            InitializeComponent();
            toolStripComboBox1.SelectedIndex = 4;
        }
        void loadDisk(string name)
        {
            treeView1.Nodes.Clear();
            listView1.Items.Clear();
            if (vol != null) vol.Close();
            vol = new FATVolume(name);
            treeView1.Nodes.Add(new TreeNode(name.Contains("\\") ? name.Substring(name.LastIndexOf('\\') + 1) : name.Contains("/") ? name.Substring(name.LastIndexOf('/') + 1) : name, 2, 2));
            getDirs(string.Empty, treeView1.Nodes[0]);
            treeView1.Nodes[0].Expand();
            FileInfo fi1 = new FileInfo();
            fi1.TreeNode = treeView1.Nodes[0];
            fi1.FullPath = "";
            fi1.FileName = "";
            treeView1.Nodes[0].Tag = fi1;
            SetDir(fi1);
            Forward = new Stack<string>();
            Back = new Stack<string>();
            toolStripButtonBack.Enabled = false;
            toolStripButtonCopy.Enabled = false;
            toolStripButtonCut.Enabled = false;
            toolStripButtonDelete.Enabled = false;
            toolStripButtonForward.Enabled = false;
            toolStripButtonNewFolder.Enabled = true;
            toolStripButtonUp.Enabled = false;
            toolStripButtonDiskProperties.Enabled = true;
            toolStripButtonImport.Enabled = true;
            toolStripButtonExport.Enabled = true;
            timer1.Start();
            statusStrip1.Items[1].Text = string.Format("{0} KB free", vol.ComputeFreeSpace()/1024);
        }
        void SetDir(FileInfo fi)
        {
            foreach (ListViewItem lvi in listView1.Items)
            {
                ((FileInfo)lvi.Tag).ListViewItem = null;
            }
            listView1.Items.Clear();
            string[] files = vol.GetFiles(fi.FullPath);
            foreach (TreeNode tn in fi.TreeNode.Nodes)
            {
                FileInfo fi1 = (FileInfo)tn.Tag;
                ListViewItem ll = new ListViewItem(tn.Text, 1, listView1.Groups[0]);
                fi1.ListViewItem = ll;
                ll.Tag = fi1;
                listView1.Items.Add(ll);
            }
            foreach (string s in files)
            {
                FileInfo fi1 = new FileInfo();
                fi1.ListViewItem = new ListViewItem(s, 0, listView1.Groups[1]);
                fi1.FileName = s;
                fi1.FullPath = fi.FullPath + "\\" + s;
                fi1.ListViewItem.Tag = fi1;
                listView1.Items.Add(fi1.ListViewItem);
            }
            statusStrip1.Items[0].Text = string.Format("{0} item(s)", listView1.Items.Count);
            toolStripComboBoxPath.Text = fi.FullPath;
        }
        void getDirs(string dir, TreeNode n)
        {
            string[] dirs = vol.GetDirectories(dir);
            //string[] files = vol.GetFiles(dir);
            foreach (string s in dirs)
            {
                TreeNode nn = new TreeNode(s, 1, 1);
                FileInfo fi = new FileInfo();
                fi.FileName = s;
                fi.FullPath = dir + "\\" + s;
                fi.TreeNode = nn;
                nn.Tag = fi;
                n.Nodes.Add(nn);
                getDirs(dir + "\\" + s, nn);
            }
            //foreach (string s in files)
            //{
                //TreeNode nn = new TreeNode(s, 0, 0);
                //FileInfo fi = new FileInfo();
                //fi.FileName = s;
                //fi.FullPath = dir + "\\" + s;
                //fi.TreeNode = nn;
                //nn.Tag = fi;
                //n.Nodes.Add(nn);
            //}
        }

        private void listView1_ItemActivate(object sender, EventArgs e)
        {
            if (listView1.SelectedItems[0].ImageIndex == 1)
                SetDir((FileInfo)listView1.SelectedItems[0].Tag);
            else
            {
                string t = statusStrip1.Items[0].Text;
                statusStrip1.Items[0].Text = string.Format("Extracting '{0}'...", ((FileInfo)listView1.SelectedItems[0].Tag).FileName);
                statusStrip1.Refresh();
                string fname = tempFol + "\\" + ((FileInfo)listView1.SelectedItems[0].Tag).FileName;
                System.IO.Stream s = vol.OpenFile(((FileInfo)listView1.SelectedItems[0].Tag).FullPath, System.IO.FileAccess.Read, System.IO.FileMode.Open);
                byte[] b = new byte[s.Length];
                s.Read(b, 0, b.Length);
                System.IO.File.WriteAllBytes(fname, b);
                System.Diagnostics.ProcessStartInfo si = new System.Diagnostics.ProcessStartInfo(fname);
                si.UseShellExecute = true;
                si.ErrorDialog = true;
                FormWindowState ws = WindowState;
                System.Diagnostics.Process p;
                try
                {
                    p = System.Diagnostics.Process.Start(fname);
                    WindowState = FormWindowState.Minimized;
                    p.WaitForExit();
                    WindowState = ws;
                }
                catch (Win32Exception ex)
                {
                    MessageBox.Show(ex.Message, "vgui", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                finally
                {
                    statusStrip1.Items[0].Text = t;
                }
            }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                loadDisk(ofd.FileName);
            }
        }

        private void treeView1_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node.ImageIndex != 0 && e.Node.Tag != null) SetDir((FileInfo)e.Node.Tag);
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                loadDisk(ofd.FileName);
            }
        }

        private void listView1_DragLeave(object sender, EventArgs e)
        {
            //DataObject data = new DataObject();
            //DoDragDrop(data, DragDropEffects.Copy);
            //System.Collections.Specialized.StringCollection sc = new System.Collections.Specialized.StringCollection();
            //sc.Add("C:\\abc");
            //data.SetFileDropList(sc);
            //DoDragDrop("Hello", DragDropEffects.Copy);
        }

        private void listView1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Move;
            else
                e.Effect = DragDropEffects.None;

        }

        private void listView1_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                MessageBox.Show(String.Join("\n",(string[])e.Data.GetData(DataFormats.FileDrop)));
        }

        private void listView1_MouseDown(object sender, MouseEventArgs e)
        {
            /*DataObject data = new DataObject();
            System.Collections.Specialized.StringCollection paths = new System.Collections.Specialized.StringCollection();
            paths.Add("C:\\abc");
            data.SetFileDropList(paths);
            DoDragDrop(data, DragDropEffects.Copy);*/
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Random r = new Random(Environment.TickCount);
            tempFol = System.IO.Directory.CreateDirectory(Environment.GetEnvironmentVariable("TMP") + "\\vguiExport" + r.Next().ToString("x")).FullName;
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            System.IO.Directory.Delete(tempFol, true);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (treeView1.Nodes.Count != 0)
                toolStripButtonPaste.Enabled = Clipboard.ContainsFileDropList();
        }

        private void toolStripButtonFolders_Click(object sender, EventArgs e)
        {
            panelFolders.Visible = toolStripButtonFolders.Checked;
        }

        private void listView1_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            bool selected = listView1.SelectedIndices.Count != 0;
            toolStripButtonCopy.Enabled = selected;
            toolStripButtonCut.Enabled = selected;
            toolStripButtonDelete.Enabled = selected;
            exportSelectedFilesToolStripMenuItem.Enabled = selected;
            statusStrip1.Items[0].Text = selected ? string.Format("{0} item(s) selected", listView1.SelectedItems.Count) : string.Format("{0} item(s)", listView1.Items.Count);
        }

        private void toolStripComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (toolStripComboBox1.SelectedIndex)
            {
                case 0:
                    listView1.View = View.Details;
                    break;
                case 1:
                    listView1.View = View.List;
                    break;
                case 2:
                    listView1.View = View.SmallIcon;
                    break;
                case 3:
                    listView1.View = View.LargeIcon;
                    break;
                case 4:
                    listView1.View = View.Tile;
                    break;
            }
        }

        private void toolStripButtonDelete_Click(object sender, EventArgs e)
        {
            ListView.SelectedListViewItemCollection lc = listView1.SelectedItems;
            if (MessageBox.Show(String.Format("Are you sure you want to delete {0}?", lc.Count == 1 ? string.Format("'{0}'", ((FileInfo)lc[0].Tag).FileName) : "these items"), "vgui", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No) return;
            foreach (ListViewItem li in lc)
            {
                FileInfo fi = (FileInfo)li.Tag;
                listView1.Items.Remove(li);
                if (fi.TreeNode != null) fi.TreeNode.Remove();
                vol.Delete(fi.FullPath);
            }
        }

        private void listView1_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (listView1.SelectedItems.Count == 0) return;
            DataObject data = new DataObject();
            System.Collections.Specialized.StringCollection paths = new System.Collections.Specialized.StringCollection();
            string t = statusStrip1.Items[0].Text;
            foreach (ListViewItem li in listView1.SelectedItems)
            {
                statusStrip1.Items[0].Text = string.Format("Extracting '{0}'...", ((FileInfo)li.Tag).FileName);
                statusStrip1.Refresh();
                string fname = tempFol + "\\" + ((FileInfo)li.Tag).FileName;
                System.IO.Stream s = vol.OpenFile(((FileInfo)li.Tag).FullPath, System.IO.FileAccess.Read, System.IO.FileMode.Open);
                byte[] b = new byte[s.Length];
                s.Read(b, 0, b.Length);
                System.IO.File.WriteAllBytes(fname, b);
                paths.Add(fname);
            }
            statusStrip1.Items[0].Text = t;
            data.SetFileDropList(paths);
            DoDragDrop(data, DragDropEffects.Copy);
        }
    }
    class FileInfo
    {
        public string FileName;
        public string FullPath;
        public ListViewItem ListViewItem;
        public TreeNode TreeNode;
    }
}
