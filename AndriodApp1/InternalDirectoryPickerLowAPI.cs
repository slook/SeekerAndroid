﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
//using AndroidX.AppCompat.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.IO;
using Environment = Android.OS.Environment;
//using AlertDialog = AndroidX.AppCompat.App.AlertDialog;

namespace AndriodApp1
{
    public class SimpleFileDialog
    {
        private AutoResetEvent _autoResetEvent;
        private static int _fileOpen = 0;
        private static int _fileSave = 1;
        private static int _folderChoose = 2;
        private int _selectType = _fileSave;
        private String _mSdcardDirectory = "";
        private Activity _mContext;
        private TextView _mTitleView1;
        private TextView _mTitleView;
        public String DefaultFileName = "default.txt";
        private String _selectedFileName = "default.txt";
        private EditText _inputText;

        private String _mDir = "";
        private List<String> _mSubdirs = null;
        private ArrayAdapter<String> _mListAdapter = null;
        private bool _mGoToUpper = false;
        private AlertDialog _dirsDialog;

        //////////////////////////////////////////////////////
        // Callback interface for selected directory
        //////////////////////////////////////////////////////


        public SimpleFileDialog(Activity context, FileSelectionMode mode)
        {
            switch (mode)
            {
                case FileSelectionMode.FileOpen:
                    _selectType = _fileOpen;
                    break;
                case FileSelectionMode.FileSave:
                    _selectType = _fileSave;
                    break;
                case FileSelectionMode.FolderChoose:
                    _selectType = _folderChoose;
                    break;
                case FileSelectionMode.FileOpenRoot:
                    _selectType = _fileOpen;
                    _mGoToUpper = true;
                    break;
                case FileSelectionMode.FileSaveRoot:
                    _selectType = _fileSave;
                    _mGoToUpper = true;
                    break;
                case FileSelectionMode.FolderChooseRoot:
                    _selectType = _folderChoose;
                    _mGoToUpper = true;
                    break;
                default:
                    _selectType = _fileOpen;
                    break;
            }

            _mContext = context;
            
            Java.IO.File[] externalRootDirs = context.GetExternalFilesDirs(null);
            if(externalRootDirs.Count()>1) //this means user has SDCARD
            {
                //get root (I assume this is going to be /storage/ but just in case)
                string[] folderNames = externalRootDirs[0].AbsolutePath.Split('/');
                string rootFolder = "storage";
                foreach (string foldername in folderNames)
                {
                    if(foldername!=string.Empty)
                    {
                        rootFolder = foldername;
                        break;
                    }
                }
                if(rootFolder!="storage")
                {
                    MainActivity.LogFirebase("sdcard root is: " + rootFolder);
                }
                _mSdcardDirectory = @"/" + rootFolder + @"/";
                //this allows one to press '..' to go above the typical emulated root.
            }
            else
            {
                _mSdcardDirectory = Environment.ExternalStorageDirectory.AbsolutePath;
            }
            
            try
            {
                _mSdcardDirectory = new File(_mSdcardDirectory).CanonicalPath;
            }
            catch (IOException ioe)
            {
            }
        }

        public enum FileSelectionMode
        {
            FileOpen,
            FileSave,
            FolderChoose,
            FileOpenRoot,
            FileSaveRoot,
            FolderChooseRoot

        }


        public async Task<string> GetFileOrDirectoryAsync(String dir)
        {
            File dirFile = new File(dir);
            while (!dirFile.Exists() || !dirFile.IsDirectory)
            {
                dir = dirFile.Parent;
                dirFile = new File(dir);
                Log.Debug("~~~~~", "dir=" + dir);
            }
            Log.Debug("~~~~~", "dir=" + dir);
            //m_sdcardDirectory
            try
            {
                dir = new File(dir).CanonicalPath;
            }
            catch (IOException ioe)
            {
                return _result;
            }

            _mDir = dir;
            _mSubdirs = GetDirectories(dir);

            AlertDialog.Builder dialogBuilder = CreateDirectoryChooserDialog(dir, _mSubdirs, (sender, args) =>
            {
                String mDirOld = _mDir;
                String sel = "" + ((AlertDialog)sender).ListView.Adapter.GetItem(args.Which);
                if (sel[sel.Length - 1] == '/') sel = sel.Substring(0, sel.Length - 1);

                // Navigate into the sub-directory
                GoToSubDir(sel, mDirOld);
                UpdateDirectory();
            });
            dialogBuilder.SetPositiveButton(Resource.String.okay, (sender, args) =>
            {
                // Current directory chosen
                // Call registered listener supplied with the chosen directory

                {
                    if (_selectType == _fileOpen || _selectType == _fileSave)
                    {
                        _selectedFileName = _inputText.Text + "";
                        _result = _mDir + "/" + _selectedFileName;
                        _autoResetEvent.Set();
                    }
                    else
                    {
                        _result = _mDir;
                        _autoResetEvent.Set();
                    }
                }

            });
            dialogBuilder.SetNegativeButton(Resource.String.cancel, (sender, args) => { });
            var backListener = new DialogBackListener();
            backListener.FileDialog = this;
            dialogBuilder.SetOnKeyListener(backListener);
            _dirsDialog = dialogBuilder.Create();

            _dirsDialog.CancelEvent += (sender, args) => { _autoResetEvent.Set(); };
            _dirsDialog.DismissEvent += (sender, args) => { _autoResetEvent.Set(); };
            // Show directory chooser dialog
            _autoResetEvent = new AutoResetEvent(false);
            _dirsDialog.Show();

            await Task.Run(() => { _autoResetEvent.WaitOne(); });

            return _result;
        }


        public class DialogBackListener : Java.Lang.Object, IDialogInterfaceOnKeyListener
        {
            public SimpleFileDialog FileDialog = null;
            public bool OnKey(IDialogInterface dialog, [GeneratedEnum] Keycode keyCode, KeyEvent e)
            {
                if(keyCode==Keycode.Back && e.Action == KeyEventActions.Up)
                {
                    if(this.FileDialog.AtRoot())
                    {
                        return false;
                    }
                    this.FileDialog.GoToSubDir("..", string.Empty);
                    this.FileDialog.UpdateDirectory();
                    return true;
                }
                return false;
            }
        }


        private string _result = null;

        private bool CreateSubDir(String newDir)
        {
            File newDirFile = new File(newDir);
            if (!newDirFile.Exists()) return newDirFile.Mkdir();
            else return false;
        }

        public bool AtRoot()
        {
            return !((_mGoToUpper || !_mDir.Equals(_mSdcardDirectory)) && !"/".Equals(_mDir));
        }

        private List<String> GetDirectories(String dir)
        {
            List<String> dirs = new List<String>();
            try
            {
                File dirFile = new File(dir);

                // if directory is not the base sd card directory add ".." for going up one directory
                if ((_mGoToUpper || !_mDir.Equals(_mSdcardDirectory)) && !"/".Equals(_mDir))
                {
                    dirs.Add("..");
                }
                Log.Debug("~~~~", "m_dir=" + _mDir);
                if (!dirFile.Exists() || !dirFile.IsDirectory)
                {
                    return dirs;
                }

                foreach (File file in dirFile.ListFiles())
                {
                    if (file.IsDirectory)
                    {
                        // Add "/" to directory names to identify them in the list
                        dirs.Add(file.Name + "/");
                    }
                    else if (_selectType == _fileSave || _selectType == _fileOpen)
                    {
                        // Add file names to the list if we are doing a file save or file open operation
                        dirs.Add(file.Name);
                    }
                }
            }
            catch (Exception e) { }

            dirs.Sort();
            return dirs;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////
        //////                                   START DIALOG DEFINITION                                    //////
        //////////////////////////////////////////////////////////////////////////////////////////////////////////
        private AlertDialog.Builder CreateDirectoryChooserDialog(String title, List<String> listItems, EventHandler<DialogClickEventArgs> onClickListener)
        {
            AlertDialog.Builder dialogBuilder = new AlertDialog.Builder(_mContext);
            DisplayMetrics displayMetrics = new DisplayMetrics();
            _mContext.WindowManager.DefaultDisplay.GetMetrics(displayMetrics);
            float density = displayMetrics.Density; //I assume this is like 1 or 2
            ////////////////////////////////////////////////
            // Create title text showing file select type // 
            ////////////////////////////////////////////////
            _mTitleView1 = new TextView(_mContext);
            _mTitleView1.LayoutParameters = new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            //m_titleView1.setTextAppearance(m_context, android.R.style.TextAppearance_Large);
            //m_titleView1.setTextColor( m_context.getResources().getColor(android.R.color.black) );

            if (_selectType == _fileOpen) _mTitleView1.Text = "Open"; //unused
            if (_selectType == _fileSave) _mTitleView1.Text = "Save As";//unused
            if (_selectType == _folderChoose) _mTitleView1.Text = this._mContext.GetString(Resource.String.select_folder);

            //need to make this a variable Save as, Open, Select Directory
            _mTitleView1.Gravity = GravityFlags.CenterVertical;
            //_mTitleView1.SetBackgroundColor(Color.DarkGray); // dark gray 	-12303292
            _mTitleView1.SetTextColor(_mContext.Resources.GetColor(Resource.Color.normalTextColor));
            _mTitleView1.SetPadding((int)Math.Ceiling(10*density), (int)Math.Ceiling(10 * density), 0, (int)Math.Ceiling(15 * density));
            _mTitleView1.SetTextSize(ComplexUnitType.Dip, 18);
            _mTitleView1.SetTypeface(null, TypefaceStyle.Bold);
            // Create custom view for AlertDialog title
            LinearLayout titleLayout1 = new LinearLayout(_mContext);
            titleLayout1.Orientation = Orientation.Vertical;
            titleLayout1.AddView(_mTitleView1);

            if (_selectType == _fileSave)
            {
                ///////////////////////////////
                // Create New Folder Button  //
                ///////////////////////////////
                Button newDirButton = new Button(_mContext);
                newDirButton.LayoutParameters = new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
                newDirButton.Text = "New Folder";
                newDirButton.Click += (sender, args) =>
                {
                    EditText input = new EditText(_mContext);
                    new AlertDialog.Builder(_mContext).SetTitle("New Folder Name").SetView(input).SetPositiveButton("OK", (o, eventArgs) =>
                    {
                        String newDirName = input.Text;
                        // Create new directory
                        if (CreateSubDir(_mDir + "/" + newDirName))
                        {
                            // Navigate into the new directory
                            _mDir += "/" + newDirName;
                            UpdateDirectory();
                        }
                        else
                        {
                            Toast.MakeText(_mContext, "Failed to create '" + newDirName + "' folder", ToastLength.Short).Show();
                        }
                    }).SetNegativeButton("Cancel", (o, eventArgs) => { }).Show();
                };
                titleLayout1.AddView(newDirButton);
            }
            /////////////////////////////////////////////////////
            // Create View with folder path and entry text box // 
            /////////////////////////////////////////////////////
            LinearLayout titleLayout = new LinearLayout(_mContext);
            titleLayout.Orientation = Orientation.Vertical;



            var currentSelection = new TextView(_mContext);
            currentSelection.LayoutParameters = new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            currentSelection.SetTextColor(_mContext.Resources.GetColor(Resource.Color.normalTextColor));
            currentSelection.Gravity = GravityFlags.CenterVertical;
            currentSelection.Text = _mContext.GetString(Resource.String.current_location_);
            currentSelection.SetPadding((int)Math.Ceiling(10 * density), (int)Math.Ceiling(5 * density), 0, (int)Math.Ceiling(3 * density));
            currentSelection.SetTextSize(ComplexUnitType.Dip, 12);
            currentSelection.SetTypeface(null, TypefaceStyle.Bold);

            titleLayout.AddView(currentSelection);

            _mTitleView = new TextView(_mContext);
            _mTitleView.LayoutParameters = new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            _mTitleView.SetTextColor(_mContext.Resources.GetColor(Resource.Color.normalTextColor));
            _mTitleView.Gravity = GravityFlags.CenterVertical;
            _mTitleView.Text = title;
            _mTitleView.SetPadding((int)Math.Ceiling(10 * density), 0, (int)Math.Ceiling(10 * density), (int)Math.Ceiling(5 * density));
            _mTitleView.SetTextSize(ComplexUnitType.Dip, 10);
            _mTitleView.SetTypeface(null, TypefaceStyle.Normal);

            titleLayout.AddView(_mTitleView);

            if (_selectType == _fileOpen || _selectType == _fileSave)
            {
                _inputText = new EditText(_mContext);
                _inputText.Text = DefaultFileName;
                titleLayout.AddView(_inputText);
            }
            //////////////////////////////////////////
            // Set Views and Finish Dialog builder  //
            //////////////////////////////////////////
            dialogBuilder.SetView(titleLayout);
            dialogBuilder.SetCustomTitle(titleLayout1);
            _mListAdapter = CreateListAdapter(listItems);
            dialogBuilder.SetSingleChoiceItems(_mListAdapter, -1, onClickListener);
            dialogBuilder.SetCancelable(true);
            return dialogBuilder;
        }

        public void GoToSubDir(string sel, string mDirOld)
        {
            // Navigate into the sub-directory
            if (sel.Equals(".."))
            {
                _mDir = _mDir.Substring(0, _mDir.LastIndexOf("/"));
                if ("".Equals(_mDir))
                {
                    _mDir = "/";
                }
            }
            else
            {
                _mDir += "/" + sel;
            }
            _selectedFileName = DefaultFileName;

            if ((new File(_mDir).IsFile)) // If the selection is a regular file
            {
                _mDir = mDirOld;
                _selectedFileName = sel;
            }
        }

        public void UpdateDirectory()
        {
            _mSubdirs.Clear();
            _mSubdirs.AddRange(GetDirectories(_mDir));
            _mTitleView.Text = _mDir;
            _dirsDialog.ListView.Adapter = null;
            _dirsDialog.ListView.Adapter = CreateListAdapter(_mSubdirs);
            //#scorch
            if (_selectType == _fileSave || _selectType == _fileOpen)
            {
                _inputText.Text = _selectedFileName;
            }
        }

        private ArrayAdapter<String> CreateListAdapter(List<String> items)
        {
            var adapter = new SimpleArrayAdaper(_mContext, Android.Resource.Layout.SelectDialogItem, Android.Resource.Id.Text1, items);
            return adapter;
        }

    }

    internal class SimpleArrayAdaper : ArrayAdapter<String>
    {
        public SimpleArrayAdaper(IntPtr handle, JniHandleOwnership transfer) : base(handle, transfer)
        {
        }

        public SimpleArrayAdaper(Context context, int textViewResourceId) : base(context, textViewResourceId)
        {
        }

        public SimpleArrayAdaper(Context context, int resource, int textViewResourceId) : base(context, resource, textViewResourceId)
        {
        }

        public SimpleArrayAdaper(Context context, int textViewResourceId, string[] objects) : base(context, textViewResourceId, objects)
        {
        }

        public SimpleArrayAdaper(Context context, int resource, int textViewResourceId, string[] objects) : base(context, resource, textViewResourceId, objects)
        {
        }

        public SimpleArrayAdaper(Context context, int textViewResourceId, IList<string> objects) : base(context, textViewResourceId, objects)
        {
        }

        public SimpleArrayAdaper(Context context, int resource, int textViewResourceId, IList<string> objects) : base(context, resource, textViewResourceId, objects)
        {
        }

        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            View v = base.GetView(position, convertView, parent);
            if (v is TextView)
            {
                // Enable list item (directory) text wrapping
                TextView tv = (TextView)v;
                tv.LayoutParameters.Height = ViewGroup.LayoutParams.WrapContent;
                tv.Ellipsize = null;
            }
            return v;
        }
    }
}