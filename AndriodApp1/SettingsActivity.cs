﻿/*
 * Copyright 2021 Seeker
 *
 * This file is part of Seeker
 *
 * Seeker is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * Seeker is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with Seeker. If not, see <http://www.gnu.org/licenses/>.
 */

using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using Android.Support.V4.Provider;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Google.Android.Material.Snackbar;
using System.Threading.Tasks;
using Android.Content.PM;

namespace AndriodApp1
{
    [Activity(Label = "SettingsActivity", Theme = "@style/AppTheme.NoActionBar")]
    public class SettingsActivity : Android.Support.V7.App.AppCompatActivity //AppCompatActivity is needed to support chaning light / dark mode programmatically...
    {
        private int CHANGE_WRITE_EXTERNAL = 0x909;
        private int CHANGE_WRITE_EXTERNAL_LEGACY = 0x910;
        
        private int UPLOAD_DIR_CHANGE_WRITE_EXTERNAL = 0x911;
        private int UPLOAD_DIR_CHANGE_WRITE_EXTERNAL_LEGACY = 0x912;

        private int READ_EXTERNAL_FOR_MEDIA_STORE = 1182021;

        private int CHANGE_INCOMPLETE_EXTERNAL = 0x913;
        private int CHANGE_INCOMPLETE_EXTERNAL_LEGACY = 0x914;

        private List<Tuple<int,int>> positionNumberPairs = new List<Tuple<int, int>>();
        private CheckBox allowPrivateRoomInvitations;

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            //MenuInflater.Inflate(Resource.Menu.account_menu, menu); //no menu for us..
            return base.OnCreateOptionsMenu(menu);
        }


        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            switch (item.ItemId)
            {
                case Android.Resource.Id.Home:
                    OnBackPressed();
                    return true;
            }
            return base.OnOptionsItemSelected(item);
        }

        protected override void OnResume()
        {
            base.OnResume();

            UPnpManager.Instance.SearchFinished += UpnpSearchFinished;
            UPnpManager.Instance.SearchStarted += UpnpSearchStarted;
            UPnpManager.Instance.DeviceSuccessfullyMapped += UpnpDeviceMapped;
            PrivilegesManager.Instance.PrivilegesChecked += PrivilegesChecked;

            //when you open up the directory selection with OpenDocumentTree the SettingsActivity is paused
            this.UpdateDirectoryViews();

            //however with the api<21 it is not paused and so an event is needed.
            SoulSeekState.DirectoryUpdatedEvent += DirectoryUpdated;

        }

        private void PrivilegesChecked(object sender, EventArgs e)
        {
            SoulSeekState.ActiveActivityRef.RunOnUiThread(() => {
                SetPrivStatusView(this.FindViewById<TextView>(Resource.Id.privStatusView));
            });
        }

        private void UpnpDeviceMapped(object sender, EventArgs e)
        {
            SoulSeekState.ActiveActivityRef.RunOnUiThread(() => {
            Toast.MakeText(this, Resource.String.upnp_success, ToastLength.Short).Show();
            SetUpnpStatusView(this.FindViewById<ImageView>(Resource.Id.UPnPStatus));
            });
        }

        private void UpnpSearchFinished(object sender, EventArgs e)
        {
            SoulSeekState.ActiveActivityRef.RunOnUiThread(() => {
                if (SoulSeekState.ListenerEnabled && SoulSeekState.ListenerUPnpEnabled && UPnpManager.Instance.RunningStatus == UpnpRunningStatus.Finished && UPnpManager.Instance.DiagStatus != UpnpDiagStatus.Success)
                {
                    Toast.MakeText(this, Resource.String.upnp_search_finished, ToastLength.Short).Show();
                }
                SetUpnpStatusView(this.FindViewById<ImageView>(Resource.Id.UPnPStatus));
            });
        }

        private void DirectoryUpdated(object sender, EventArgs e)
        {
            UpdateDirectoryViews();
        }

        private void UpdateDirectoryViews()
        {
            this.SetIncompleteFolderView();
            this.SetCompleteFolderView();
            this.SetSharedFolderView();
        }

        private void UpnpSearchStarted(object sender, EventArgs e)
        {
            SoulSeekState.ActiveActivityRef.RunOnUiThread(() => {
                SetUpnpStatusView(this.FindViewById<ImageView>(Resource.Id.UPnPStatus));
            });
        }

        protected override void OnPause()
        {

            UPnpManager.Instance.SearchFinished -= UpnpSearchFinished;
            UPnpManager.Instance.SearchStarted -= UpnpSearchStarted;
            UPnpManager.Instance.DeviceSuccessfullyMapped -= UpnpDeviceMapped;
            PrivilegesManager.Instance.PrivilegesChecked -= PrivilegesChecked;
            SoulSeekState.DirectoryUpdatedEvent -= DirectoryUpdated;
            SettingsActivity.SaveAdditionalDirectorySettingsToSharedPreferences();
            base.OnPause();
        }

        //private string SaveDataDirectoryUri = string.Empty;

        private CheckBox createUsernameSubfoldersView;
        private CheckBox createCompleteAndIncompleteFoldersView;
        private CheckBox manuallyChooseIncompleteFolderView;
        private TextView currentCompleteFolderView;
        private TextView currentIncompleteFolderView;
        private TextView currentSharedFolderView;

        private ViewGroup incompleteFolderViewLayout;
        private Button changeIncompleteDirectory;

        private ViewGroup sharingSubLayout1;
        private ViewGroup sharingSubLayout2;

        private ViewGroup listeningSubLayout2;
        private ViewGroup listeningSubLayout3;

        Button sharedFolderButton;
        Button browseSelfButton;
        Button rescanSharesButton;

        Button checkStatus;
        Button changePort;

        CheckBox useUPnPCheckBox;


        protected override void OnCreate(Bundle savedInstanceState)
        {
            MainActivity.LogDebug("Settings Created");



            base.OnCreate(savedInstanceState);



            SoulSeekState.ActiveActivityRef = this;
            SetContentView(Resource.Layout.settings_layout);

            Android.Support.V7.Widget.Toolbar myToolbar = (Android.Support.V7.Widget.Toolbar)FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.setting_toolbar);
            myToolbar.InflateMenu(Resource.Menu.search_menu);
            myToolbar.Title = this.GetString(Resource.String.settings);
            this.SetSupportActionBar(myToolbar);
            this.SupportActionBar.SetDisplayHomeAsUpEnabled(true);
            

            Intent intent = Intent; //intent that started this activity
            //this.SaveDataDirectoryUri = intent.GetStringExtra("SaveDataDirectoryUri");
            Button changeDirSettings = FindViewById<Button>(Resource.Id.changeDirSettings);
            changeDirSettings.Click += ChangeDownloadDirectory;

            CheckBox autoClearComplete = FindViewById<CheckBox>(Resource.Id.autoClearComplete);
            autoClearComplete.Checked = SoulSeekState.AutoClearCompleteDownloads;
            autoClearComplete.CheckedChange += AutoClearComplete_CheckedChange;

            CheckBox autoClearCompleteUploads = FindViewById<CheckBox>(Resource.Id.autoClearCompleteUploads);
            autoClearCompleteUploads.Checked = SoulSeekState.AutoClearCompleteUploads;
            autoClearCompleteUploads.CheckedChange += AutoClearCompleteUploads_CheckedChange;

            CheckBox freeUploadSlotsOnly = FindViewById<CheckBox>(Resource.Id.freeUploadSlots);
            freeUploadSlotsOnly.Checked = SoulSeekState.FreeUploadSlotsOnly;
            freeUploadSlotsOnly.CheckedChange += FreeUploadSlotsOnly_CheckedChange;

            allowPrivateRoomInvitations = FindViewById<CheckBox>(Resource.Id.allowPrivateRoomInvitations);
            allowPrivateRoomInvitations.Checked = SoulSeekState.AllowPrivateRoomInvitations;
            allowPrivateRoomInvitations.CheckedChange += AllowPrivateRoomInvitations_CheckedChange;

            CheckBox disableDownloadNotification = FindViewById<CheckBox>(Resource.Id.disableToastNotificationOnDownload);
            disableDownloadNotification.Checked = SoulSeekState.DisableDownloadToastNotification;
            disableDownloadNotification.CheckedChange += DisableDownloadNotification_CheckedChange;

            CheckBox memoryFileDownloadSwitchCheckBox = FindViewById<CheckBox>(Resource.Id.memoryFileDownloadSwitchCheckBox);
            memoryFileDownloadSwitchCheckBox.Checked = !SoulSeekState.MemoryBackedDownload;
            memoryFileDownloadSwitchCheckBox.CheckedChange += MemoryFileDownloadSwitchCheckBox_CheckedChange;

            ImageView memoryFileDownloadSwitchIcon = FindViewById<ImageView>(Resource.Id.memoryFileDownloadSwitchIcon);
            memoryFileDownloadSwitchIcon.Click += MemoryFileDownloadSwitchIcon_Click;

            //Button closeButton = FindViewById<Button>(Resource.Id.closeSettings);
            //closeButton.Click += CloseButton_Click;

            Button restoreDefaultsButton = FindViewById<Button>(Resource.Id.restoreDefaults);
            restoreDefaultsButton.Click += RestoreDefaults_Click;

            Button clearHistory = FindViewById<Button>(Resource.Id.clearHistory);
            clearHistory.Click += ClearHistory_Click;

            CheckBox rememberSearchHistory = FindViewById<CheckBox>(Resource.Id.searchHistoryRemember);
            rememberSearchHistory.Checked = SoulSeekState.RememberSearchHistory;
            rememberSearchHistory.CheckedChange += RememberSearchHistory_CheckedChange;

            Button clearRecentUserHistory = FindViewById<Button>(Resource.Id.clearRecentUsers);
            clearRecentUserHistory.Click += ClearRecentUserHistory_Click; ;

            CheckBox rememberRecentUsers = FindViewById<CheckBox>(Resource.Id.rememberRecentUsers);
            rememberRecentUsers.Checked = SoulSeekState.ShowRecentUsers;
            rememberRecentUsers.CheckedChange += RememberRecentUsers_CheckedChange;

            Spinner searchNumSpinner = FindViewById<Spinner>(Resource.Id.searchNumberSpinner);
            positionNumberPairs.Add(new Tuple<int, int>(0,5));
            positionNumberPairs.Add(new Tuple<int, int>(1,10));
            positionNumberPairs.Add(new Tuple<int, int>(2,15));
            positionNumberPairs.Add(new Tuple<int, int>(3,30));
            positionNumberPairs.Add(new Tuple<int, int>(4,50));
            positionNumberPairs.Add(new Tuple<int, int>(5,100));
            positionNumberPairs.Add(new Tuple<int, int>(6,250));
            String[] options = new String[]{ positionNumberPairs[0].Item2.ToString(),
                                             positionNumberPairs[1].Item2.ToString(),
                                             positionNumberPairs[2].Item2.ToString(),
                                             positionNumberPairs[3].Item2.ToString(),
                                             positionNumberPairs[4].Item2.ToString(),
                                             positionNumberPairs[5].Item2.ToString(),
                                             positionNumberPairs[6].Item2.ToString(),
                };
            ArrayAdapter<String> searchNumOptions = new ArrayAdapter<string>(this,Resource.Layout.support_simple_spinner_dropdown_item, options);
            searchNumSpinner.Adapter = searchNumOptions;
            SetSpinnerPosition(searchNumSpinner);
            searchNumSpinner.ItemSelected += SearchNumSpinner_ItemSelected;

            Spinner dayNightMode = FindViewById<Spinner>(Resource.Id.nightModeSpinner);
            dayNightMode.ItemSelected -= DayNightMode_ItemSelected;
            String[] dayNightOptionsStrings = new String[]{ this.GetString(Resource.String.follow_system), this.GetString(Resource.String.always_light), this.GetString(Resource.String.always_dark) };
            ArrayAdapter<String> dayNightOptions = new ArrayAdapter<string>(this, Resource.Layout.support_simple_spinner_dropdown_item, dayNightOptionsStrings);
            dayNightMode.Adapter = dayNightOptions;
            SetSpinnerPositionDayNight(dayNightMode);
            dayNightMode.ItemSelected += DayNightMode_ItemSelected;

            ImageView imageView = this.FindViewById<ImageView>(Resource.Id.sharedStatus);
            imageView.Click += ImageView_Click;
            UpdateShareImageView();

            sharedFolderButton = FindViewById<Button>(Resource.Id.setSharedFolder);
            sharedFolderButton.Click += ChangeUploadDirectory;
            currentSharedFolderView = FindViewById<TextView>(Resource.Id.sharedFolderPath);

            CheckBox shareCheckBox = FindViewById<CheckBox>(Resource.Id.enableSharing);
            shareCheckBox.Checked = SoulSeekState.SharingOn;
            shareCheckBox.CheckedChange += ShareCheckBox_CheckedChange;

            ImageView moreInfoButton = FindViewById<ImageView>(Resource.Id.moreInfoButton);
            moreInfoButton.Click += MoreInfoButton_Click;

            browseSelfButton = FindViewById<Button>(Resource.Id.browseSelfButton);
            browseSelfButton.Click += BrowseSelfButton_Click;

            rescanSharesButton = FindViewById<Button>(Resource.Id.rescanShares);
            rescanSharesButton.Click += RescanSharesButton_Click;

            ImageView startupServiceMoreInfo = FindViewById<ImageView>(Resource.Id.helpServiceOnStartup);
            startupServiceMoreInfo.Click += StartupServiceMoreInfo_Click;

            CheckBox startServiceOnStartupCheckBox = FindViewById<CheckBox>(Resource.Id.startServiceOnStartupCheckBox);
            startServiceOnStartupCheckBox.Checked = SoulSeekState.StartServiceOnStartup;
            startServiceOnStartupCheckBox.CheckedChange += StartServiceOnStartupCheckBox_CheckedChange;

            Button startupServiceButton= FindViewById<Button>(Resource.Id.startServiceOnStartupButton);
            startupServiceButton.Click += StartupServiceButton_Click;
            SetButtonText(startupServiceButton);

            CheckBox enableListening = FindViewById<CheckBox>(Resource.Id.enableListening);
            enableListening.Checked = SoulSeekState.ListenerEnabled;
            enableListening.CheckedChange += EnableListening_CheckedChange;

            ImageView listeningMoreInfo  = FindViewById<ImageView>(Resource.Id.listeningHelp);
            listeningMoreInfo.Click += ListeningMoreInfo_Click;

            TextView portView = FindViewById<TextView>(Resource.Id.portView);
            SetPortViewText(portView);

            changePort = FindViewById<Button>(Resource.Id.changePort);
            changePort.Click += ChangePort_Click;

            checkStatus = FindViewById<Button>(Resource.Id.checkStatus);
            checkStatus.Click += CheckStatus_Click;

            Button getPriv = FindViewById<Button>(Resource.Id.getPriv);
            getPriv.Click += GetPriv_Click;

            Button checkPriv = FindViewById<Button>(Resource.Id.checkPriv);
            checkPriv.Click += CheckPriv_Click;

            TextView privStatus = FindViewById<TextView>(Resource.Id.privStatusView);
            SetPrivStatusView(privStatus);

            ImageView privHelp = FindViewById<ImageView>(Resource.Id.privHelp);
            privHelp.Click += PrivHelp_Click;

            Button editUserInfo = FindViewById<Button>(Resource.Id.editUserInfoButton);
            editUserInfo.Click += EditUserInfo_Click;

            useUPnPCheckBox = FindViewById<CheckBox>(Resource.Id.useUPnPCheckBox);
            useUPnPCheckBox.Checked = SoulSeekState.ListenerUPnpEnabled;
            useUPnPCheckBox.CheckedChange += UseUPnPCheckBox_CheckedChange;

            ImageView UpnpStatusView = FindViewById<ImageView>(Resource.Id.UPnPStatus);
            SetUpnpStatusView(UpnpStatusView);
            UpnpStatusView.Click += ImageView_Click;

            sharingSubLayout1 = FindViewById<ViewGroup>(Resource.Id.dlChangeSharedDirectoryLayout);
            sharingSubLayout2 = FindViewById<ViewGroup>(Resource.Id.sharingSubLayout2);
            UpdateSharingViewState();

            listeningSubLayout2 = FindViewById<ViewGroup>(Resource.Id.listeningRow2);
            listeningSubLayout3 = FindViewById<ViewGroup>(Resource.Id.listeningRow3);
            UpdateListeningViewState();

            Button importData = this.FindViewById<Button>(Resource.Id.importDataButton);
            importData.Click += ImportData_Click;

            /*
            **NOTE**
            * 
            * Regarding Directory Options (Incomplete Folder Options, Complete Folder Options):
            * Incomplete Folder internal structure is always the same (folder is username concat file foldername),
            *   it is just the placement of it that differs
            * The automatic placement is "Soulseek Incomplete" in the same directory chosen for downloads (if "Create Folders for Downloads and Incomplete" is on.
            * Otherwise the placement is in AppData Local - what Android calls "Internal Storage"
            * 
            * The incomplete folder choices are used when the stream is created and saved in IncompleteUri.  Therefore, changing this on the fly is okay, it just wont 
            *   take effect until one starts a new download.
            * The complete folder choices are used when the file is actually saved / moved.  So changing this on the fly is okay, the transfer, once finished, will just go into its new place.
            * 
            * When one turns "Manual Selection for Incomplete" off, it reverts back to Automatic.  The user will have to reselect their folder if they choose to turn it back on.
            * 
            * To clear Incomplete, there cannot be any pending transfers.  Paused transfers are okay, they will just start from the top...
            * 
            * If "Use Manual Incomplete Folder" is checked, but no Manual Incomplete Folder is chosen, then it is as if it is not checked.
            * Also its fine if its null, or no longer writable, etc.  It will just get set back to default.  User will have to re-set it on their own.
            * 
            **NOTE**
            */

            createUsernameSubfoldersView = this.FindViewById<CheckBox>(Resource.Id.createUsernameSubfolders);
            createUsernameSubfoldersView.Checked = SoulSeekState.CreateUsernameSubfolders;
            createUsernameSubfoldersView.CheckedChange += CreateUsernameSubfoldersView_CheckedChange;
            createCompleteAndIncompleteFoldersView = this.FindViewById<CheckBox>(Resource.Id.createCompleteAndIncompleteDirectories);
            createCompleteAndIncompleteFoldersView.Checked = SoulSeekState.CreateCompleteAndIncompleteFolders;
            createCompleteAndIncompleteFoldersView.CheckedChange += CreateCompleteAndIncompleteFoldersView_CheckedChange;
            manuallyChooseIncompleteFolderView = this.FindViewById<CheckBox>(Resource.Id.manuallySetIncomplete);
            manuallyChooseIncompleteFolderView.Checked = SoulSeekState.OverrideDefaultIncompleteLocations;
            manuallyChooseIncompleteFolderView.CheckedChange += ManuallyChooseIncompleteFolderView_CheckedChange;
            currentCompleteFolderView = this.FindViewById<TextView>(Resource.Id.completeFolderPath);
            currentIncompleteFolderView = this.FindViewById<TextView>(Resource.Id.incompleteFolderPath);

            changeIncompleteDirectory = this.FindViewById<Button>(Resource.Id.changeIncompleteDirSettings);
            changeIncompleteDirectory.Click += ChangeIncompleteDirectory;
            incompleteFolderViewLayout = this.FindViewById<ViewGroup>(Resource.Id.incompleteDirectoryLayout);


            SetIncompleteDirectoryState();
            SetCompleteFolderView();
            SetIncompleteFolderView();
            SetSharedFolderView();
        }

        private void ImportData_Click(object sender, EventArgs e)
        {
            if(!SoulSeekState.currentlyLoggedIn || !SoulSeekState.SoulseekClient.State.HasFlag(Soulseek.SoulseekClientStates.LoggedIn))
            {
                Toast.MakeText(this, "Must be logged in and connected to import data.", ToastLength.Long).Show();
                return;
            }
            Intent intent = new Intent(this, typeof(ImportWizardActivity));
            StartActivity(intent);
        }

        private void ClearRecentUserHistory_Click(object sender, EventArgs e)
        {
            //set to just the added users....
            int count = SoulSeekState.UserList?.Count ?? 0;
            if (count > 0)
            {
                lock(SoulSeekState.UserList)
                {
                    SoulSeekState.RecentUsersManager.SetRecentUserList(SoulSeekState.UserList.Select(uli => uli.Username).ToList());
                }
            }
            else
            {
                SoulSeekState.RecentUsersManager.SetRecentUserList(new List<string>());
            }
            SeekerApplication.SaveRecentUsers();
        }

        private void RememberRecentUsers_CheckedChange(object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            SoulSeekState.ShowRecentUsers = e.IsChecked;
        }

        private void RescanSharesButton_Click(object sender, EventArgs e)
        {
            if(SoulSeekState.IsParsing)
            {
                Toast.MakeText(this, "Already parsing", ToastLength.Long).Show();
                return;
            }
            if(string.IsNullOrEmpty(SoulSeekState.UploadDataDirectoryUri))
            {
                Toast.MakeText(this, "Directory not set", ToastLength.Long).Show();
                return;
            }
            Toast.MakeText(this, Resource.String.parsing_files_wait, ToastLength.Long).Show();

            //for rescan=true, we use the previous parse to get metadata if there is a match...
            //so that we do not have to read the file again to get things like bitrate, samples, etc.
            //if the presentable name is in the last parse, and the size matches, then use those attributes we previously had to read the file to get..
            if(SoulSeekState.PreOpenDocumentTree())
            {
                SuccessfulUploadExternalCallback(Android.Net.Uri.Parse(SoulSeekState.UploadDataDirectoryUri), -1, true, true);
            }
            else
            {
                SuccessfulUploadExternalCallback(Android.Net.Uri.Parse(SoulSeekState.UploadDataDirectoryUri), -1, false, true);
            }
        }

        private static string GetFriendlyDownloadDirectoryName()
        {
            if(SoulSeekState.RootDocumentFile==null) //even in API<21 we do set this RootDocumentFile
            {
                if(SoulSeekState.UseLegacyStorage())
                {
                    //if not set and legacy storage, then the directory is simple the default music
                    string path = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryMusic).AbsolutePath;
                    return Android.Net.Uri.Parse(new Java.IO.File(path).ToURI().ToString()).LastPathSegment;
                }
                else
                {
                    //if not set and not legacy storage, then that is bad.  user must set it.
                    return "Not Set";
                }
            }
            else
            {
                return SoulSeekState.RootDocumentFile.Uri.LastPathSegment;
            }
        }

        public static bool UseIncompleteManualFolder()
        {
            return (SoulSeekState.OverrideDefaultIncompleteLocations && SoulSeekState.RootIncompleteDocumentFile != null);
        }

        private static string GetFriendlyIncompleteDirectoryName()
        {
            if(SoulSeekState.MemoryBackedDownload)
            {
                return "Not in Use";
            }
            if(SoulSeekState.OverrideDefaultIncompleteLocations && SoulSeekState.RootIncompleteDocumentFile != null) //if doc file is null that means we could not write to it.
            {
                return SoulSeekState.RootIncompleteDocumentFile.Uri.LastPathSegment;
            }
            else
            {
                if(!SoulSeekState.CreateCompleteAndIncompleteFolders)
                {
                    return "App Local Storage";
                }
                //if not override then its whatever the download directory is...
                if (SoulSeekState.RootDocumentFile == null) //even in API<21 we do set this RootDocumentFile
                {
                    if (SoulSeekState.UseLegacyStorage())
                    {
                        //if not set and legacy storage, then the directory is simple the default music
                        string path = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryMusic).AbsolutePath;
                        return Android.Net.Uri.Parse(new Java.IO.File(path).ToURI().ToString()).LastPathSegment; //this is to prevent line breaks.
                    }
                    else
                    {
                        //if not set and not legacy storage, then that is bad.  user must set it.
                        return "Not Set";
                    }
                }
                else
                {
                    return SoulSeekState.RootDocumentFile.Uri.LastPathSegment;
                }
            }
        }

        private static string GetFriendlySharedDirectoryName()
        {
            if (!SoulSeekState.SharingOn)
            {
                return "Not Sharing";
            }
            else if (SoulSeekState.IsParsing)
            {
                return "Parsing...";
            }
            else if(SoulSeekState.UploadDataDirectoryUri == null || SoulSeekState.UploadDataDirectoryUri == string.Empty)
            {
                return "Not Set";
            }
            else
            {
                return Android.Net.Uri.Parse(SoulSeekState.UploadDataDirectoryUri).LastPathSegment;
            }
        }



        private void SetIncompleteDirectoryState()
        {
            if (SoulSeekState.OverrideDefaultIncompleteLocations)
            {
                incompleteFolderViewLayout.Enabled = true;
                changeIncompleteDirectory.Enabled = true;
                incompleteFolderViewLayout.Alpha = 1.0f;
                changeIncompleteDirectory.Alpha = 1.0f;
                changeIncompleteDirectory.Clickable = true;
            }
            else
            {
                incompleteFolderViewLayout.Enabled = false;
                changeIncompleteDirectory.Enabled = false; //this make it not clickable
                incompleteFolderViewLayout.Alpha = 0.5f;
                changeIncompleteDirectory.Alpha = 0.5f;
                changeIncompleteDirectory.Clickable = false;
            }
        }

        private void SetCompleteFolderView()
        {
            string friendlyName = Helpers.AvoidLineBreaks(GetFriendlyDownloadDirectoryName());
            currentCompleteFolderView.Text = friendlyName;
            Helpers.SetToolTipText(currentCompleteFolderView, friendlyName);
        }

        private void SetIncompleteFolderView()
        {
            string friendlyName = Helpers.AvoidLineBreaks(GetFriendlyIncompleteDirectoryName());
            currentIncompleteFolderView.Text = friendlyName;
            Helpers.SetToolTipText(currentIncompleteFolderView, friendlyName);
        }

        private void SetSharedFolderView()
        {
            string friendlyName = Helpers.AvoidLineBreaks(GetFriendlySharedDirectoryName());
            currentSharedFolderView.Text = friendlyName;
            Helpers.SetToolTipText(currentSharedFolderView, friendlyName);
        }



        //private static string GetIncompleteFolderLocation()
        //{
        //    if (SoulSeekState.OverrideDefaultIncompleteLocations)
        //    {

        //    }
        //    else
        //    {
        //        if (SoulSeekState.CreateCompleteAndIncompleteFolders)
        //        {

        //        }
        //        else
        //        {

        //        }
        //    }
        //}

        private void ManuallyChooseIncompleteFolderView_CheckedChange(object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            SoulSeekState.OverrideDefaultIncompleteLocations = e.IsChecked;
            SetIncompleteDirectoryState();
            SetIncompleteFolderView();
        }

        private void CreateCompleteAndIncompleteFoldersView_CheckedChange(object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            SoulSeekState.CreateCompleteAndIncompleteFolders = e.IsChecked;
            SetIncompleteFolderView();
        }

        private void CreateUsernameSubfoldersView_CheckedChange(object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            SoulSeekState.CreateUsernameSubfolders = e.IsChecked;
        }

        private void PrivHelp_Click(object sender, EventArgs e)
        {
            var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
            var diag = builder.SetMessage(Resource.String.privileges_more_info).SetPositiveButton(Resource.String.close, OnCloseClick).Create();
            diag.Show();
        }

        private void CheckPriv_Click(object sender, EventArgs e)
        {
            SeekerApplication.ShowToast(SeekerApplication.GetString(Resource.String.checking_priv_), ToastLength.Short);
            PrivilegesManager.Instance.GetPrivilegesAPI(true);
        }

        private void GetPriv_Click(object sender, EventArgs e)
        {
            if(SoulSeekState.Username==string.Empty || SoulSeekState.Username==null)
            {
                SeekerApplication.ShowToast(SeekerApplication.GetString(Resource.String.must_be_logged_in_to_get_privileges), ToastLength.Long);
                return;
            }
            //note: it seems that the Uri.Encode is not strictly necessary.  that is both "dog gone it" and "dog%20gone%20it" work just fine...
            Android.Net.Uri uri = Android.Net.Uri.Parse("https://www.slsknet.org/userlogin.php?username=" + Android.Net.Uri.Encode(SoulSeekState.Username)); // missing 'http://' will cause crash.
            Helpers.ViewUri(uri,this);
        }

        private void EditUserInfo_Click(object sender, EventArgs e)
        {
            Intent intent = new Intent(SoulSeekState.ActiveActivityRef, typeof(EditUserInfoActivity));
            this.StartActivity(intent);
        }

        private void UseUPnPCheckBox_CheckedChange(object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            if(e.IsChecked == SoulSeekState.ListenerUPnpEnabled)
            {
                return;
            }
            SoulSeekState.ListenerUPnpEnabled = e.IsChecked;
            SeekerApplication.SaveListeningState();

            if (e.IsChecked)
            {
                //open port...
                UPnpManager.Instance.Feedback = true;
                UPnpManager.Instance.SearchAndSetMappingIfRequired();
                
            }
            else
            {
                SetUpnpStatusView(this.FindViewById<ImageView>(Resource.Id.UPnPStatus)); //so that it shows not enabled...
            }
        }

        private void EnableListening_CheckedChange(object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            if(e.IsChecked == SoulSeekState.ListenerEnabled)
            {
                return;
            }
            if(e.IsChecked)
            {
                Toast.MakeText(SoulSeekState.ActiveActivityRef,Resource.String.enabling_listener, ToastLength.Short).Show();
            }
            else
            {
                Toast.MakeText(SoulSeekState.ActiveActivityRef, Resource.String.disabling_listener, ToastLength.Short).Show();
            }
            SoulSeekState.ListenerEnabled = e.IsChecked;
            UpdateListeningViewState();
            SeekerApplication.SaveListeningState();
            ReconfigureOptionsAPI(null, e.IsChecked, null);
            if(e.IsChecked)
            {
                UPnpManager.Instance.Feedback = true;
                UPnpManager.Instance.SearchAndSetMappingIfRequired(); //bc it may not have been set before...
            }
        }


        private void CheckStatus_Click(object sender, EventArgs e)
        {
            Android.Net.Uri uri = Android.Net.Uri.Parse("http://tools.slsknet.org/porttest.php?port=" + SoulSeekState.ListenerPort); // missing 'http://' will cause crashed. //an https for this link does not exist
            Helpers.ViewUri(uri,this);
        }
        private static AndroidX.AppCompat.App.AlertDialog changePortDialog = null;
        private void ChangePort_Click(object sender, EventArgs e)
        {
            MainActivity.LogInfoFirebase("ShowChangePortDialog");
            AndroidX.AppCompat.App.AlertDialog.Builder builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this); //failed to bind....
            builder.SetTitle(this.GetString(Resource.String.change_port) + ":");
            View viewInflated = LayoutInflater.From(this).Inflate(Resource.Layout.choose_port, (ViewGroup)this.FindViewById(Android.Resource.Id.Content), false);
            // Set up the input
            EditText input = (EditText)viewInflated.FindViewById<EditText>(Resource.Id.chosePortEditText);
            builder.SetView(viewInflated);

            EventHandler<DialogClickEventArgs> eventHandler = new EventHandler<DialogClickEventArgs>((object sender, DialogClickEventArgs okayArgs) =>
            {
                int portNum = -1;
                if(!int.TryParse(input.Text, out portNum))
                {
                    Toast.MakeText(this, Resource.String.port_failed_parse, ToastLength.Long).Show();
                    return;
                }
                if(portNum<1024 || portNum>65535)
                {
                    Toast.MakeText(this, Resource.String.port_out_of_range, ToastLength.Long).Show();
                    return;
                }
                ReconfigureOptionsAPI(null, null, portNum);
                SoulSeekState.ListenerPort = portNum;
                UPnpManager.Instance.Feedback = true;
                UPnpManager.Instance.SearchAndSetMappingIfRequired();
                SeekerApplication.SaveListeningState();
                SetPortViewText(FindViewById<TextView>(Resource.Id.portView));
                changePortDialog.Dismiss();

            });

            EventHandler<DialogClickEventArgs> cancelHandler = new EventHandler<DialogClickEventArgs>((object sender, DialogClickEventArgs okayArgs) =>
            {
                if (sender is AndroidX.AppCompat.App.AlertDialog aDiag)
                {
                    aDiag.Dismiss();
                }
                else
                {
                    changePortDialog.Dismiss();
                }

            });


            System.EventHandler<TextView.EditorActionEventArgs> editorAction = (object sender, TextView.EditorActionEventArgs e) =>
            {
                if (e.ActionId == Android.Views.InputMethods.ImeAction.Done || //in this case it is Done (blue checkmark)
                    e.ActionId == Android.Views.InputMethods.ImeAction.Go ||
                    e.ActionId == Android.Views.InputMethods.ImeAction.Next ||
                    e.ActionId == Android.Views.InputMethods.ImeAction.Search) //ImeNull if being called due to the enter key being pressed. (MSDN) but ImeNull gets called all the time....
                {
                    MainActivity.LogDebug("IME ACTION: " + e.ActionId.ToString());
                    //rootView.FindViewById<EditText>(Resource.Id.filterText).ClearFocus();
                    //rootView.FindViewById<View>(Resource.Id.focusableLayout).RequestFocus();
                    //overriding this, the keyboard fails to go down by default for some reason.....
                    try
                    {
                        Android.Views.InputMethods.InputMethodManager imm = (Android.Views.InputMethods.InputMethodManager)SoulSeekState.MainActivityRef.GetSystemService(Context.InputMethodService);
                        imm.HideSoftInputFromWindow(this.FindViewById<ViewGroup>(Android.Resource.Id.Content).WindowToken, 0);
                    }
                    catch (System.Exception ex)
                    {
                        MainActivity.LogFirebase(ex.Message + " error closing keyboard");
                    }
                    //Do the Browse Logic...
                    eventHandler(sender, null);
                }
            };

            input.EditorAction += editorAction;
            input.FocusChange += Input_FocusChange;

            builder.SetPositiveButton(Resource.String.okay, eventHandler);
            builder.SetNegativeButton(Resource.String.cancel, cancelHandler);

            changePortDialog = builder.Create();
            changePortDialog.Show();
        }

        private void Input_FocusChange(object sender, View.FocusChangeEventArgs e)
        {
            try
            {
                SoulSeekState.ActiveActivityRef.Window.SetSoftInputMode(SoftInput.AdjustNothing);
            }
            catch (System.Exception err)
            {
                MainActivity.LogFirebase("MainActivity_FocusChange" + err.Message);
            }
        }

        private static void SetPortViewText(TextView tv)
        {
            tv.Text = SoulSeekState.ActiveActivityRef.GetString(Resource.String.port) + ": " + SoulSeekState.ListenerPort.ToString();
        }

        private static void SetPrivStatusView(TextView tv)
        {
            string privileges = SeekerApplication.GetString(Resource.String.privileges) + ": ";
            tv.Text = privileges + PrivilegesManager.Instance.GetPrivilegeStatus();
        }

        private static void SetUpnpStatusView(ImageView iv)
        {
            //TODO
            Tuple< UPnpManager.ListeningIcon,string> info = UPnpManager.Instance.GetIconAndMessage();
            if(iv==null) return;
            if ((int)Android.OS.Build.VERSION.SdkInt >= 26)
            {
                iv.TooltipText = info.Item2; //api26+ otherwise crash...
            }
            else
            {
                AndroidX.AppCompat.Widget.TooltipCompat.SetTooltipText(iv, info.Item2);
            }
            switch (info.Item1)
            {
                case UPnpManager.ListeningIcon.ErrorIcon:
                    iv.SetImageResource(Resource.Drawable.lan_disconnect);
                    break;
                case UPnpManager.ListeningIcon.OffIcon:
                    iv.SetImageResource(Resource.Drawable.network_off_outline);
                    break;
                case UPnpManager.ListeningIcon.PendingIcon:
                    iv.SetImageResource(Resource.Drawable.lan_pending);
                    break;
                case UPnpManager.ListeningIcon.SuccessIcon:
                    iv.SetImageResource(Resource.Drawable.lan_connect);
                    break;
            }
        }

        private void ListeningMoreInfo_Click(object sender, EventArgs e)
        {
            var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
            var diag = builder.SetMessage(Resource.String.listening).SetPositiveButton(Resource.String.close, OnCloseClick).Create();
            diag.Show();
        }

        public const string FromBrowseSelf = "FromBrowseSelf";
        private void BrowseSelfButton_Click(object sender, EventArgs e)
        {
            if(!SoulSeekState.SharingOn || SoulSeekState.SharedFileCache == null)
            {
                Toast.MakeText(this, Resource.String.not_sharing, ToastLength.Short).Show();
                return;
            }
            if(!SoulSeekState.SharedFileCache.SuccessfullyInitialized || SoulSeekState.SharedFileCache.BrowseResponse == null)
            {
                Toast.MakeText(this, Resource.String.failed_to_parse_shares_post, ToastLength.Short).Show();
                return;
            }
            string errorMsgToToast = string.Empty;
            TreeNode<Soulseek.Directory> tree = DownloadDialog.CreateTree(SoulSeekState.SharedFileCache.BrowseResponse, false, null, null, SoulSeekState.Username, out errorMsgToToast);
            if(errorMsgToToast!=null && errorMsgToToast!=string.Empty)
            {
                Toast.MakeText(this, errorMsgToToast, ToastLength.Short).Show();
                return;
            }
            if (tree != null)
            {
                SoulSeekState.OnBrowseResponseReceived(SoulSeekState.SharedFileCache.BrowseResponse, tree, SoulSeekState.Username, null);
            }

            Intent intent = new Intent(SoulSeekState.ActiveActivityRef, typeof(MainActivity));
            intent.AddFlags(ActivityFlags.SingleTop);
            intent.PutExtra(FromBrowseSelf, 3); //the tab to go to
            this.StartActivity(intent);
        }

        private void SetButtonText(Button btn)
        {
            if(SoulSeekState.IsStartUpServiceCurrentlyRunning)
            {
                btn.Text = this.GetString(Resource.String.stop);
            }
            else
            {
                btn.Text = this.GetString(Resource.String.start);
            }
        }

        private void StartupServiceButton_Click(object sender, EventArgs e)
        {
            if (SoulSeekState.IsStartUpServiceCurrentlyRunning)
            {
                Intent seekerKeepAliveService = new Intent(this, typeof(SeekerKeepAliveService));
                this.StopService(seekerKeepAliveService);
                SoulSeekState.IsStartUpServiceCurrentlyRunning = false;
            }
            else
            {
                Intent seekerKeepAliveService = new Intent(this, typeof(SeekerKeepAliveService));
                this.StartService(seekerKeepAliveService);
                SoulSeekState.IsStartUpServiceCurrentlyRunning = true;
            }
            SetButtonText(FindViewById<Button>(Resource.Id.startServiceOnStartupButton));
        }

        private void StartServiceOnStartupCheckBox_CheckedChange(object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            SoulSeekState.StartServiceOnStartup = e.IsChecked;
            lock (MainActivity.SHARED_PREF_LOCK)
            {
                var editor = SoulSeekState.ActiveActivityRef.GetSharedPreferences("SoulSeekPrefs", 0).Edit();
                editor.PutBoolean(SoulSeekState.M_ServiceOnStartup, SoulSeekState.StartServiceOnStartup);
                bool success = editor.Commit();
            }
        }

        private void StartupServiceMoreInfo_Click(object sender, EventArgs e)
        {
            var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
            var diag = builder.SetMessage(Resource.String.keep_alive_service).SetPositiveButton(Resource.String.close, OnCloseClick).Create();
            diag.Show();
        }

        private void AllowPrivateRoomInvitations_CheckedChange(object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            if(e.IsChecked == SoulSeekState.AllowPrivateRoomInvitations)
            {
                MainActivity.LogDebug("allow private: nothing to do");
            }
            else
            {
                string newstate = e.IsChecked ? this.GetString(Resource.String.allowed) : this.GetString(Resource.String.denied);
                Toast.MakeText(SoulSeekState.ActiveActivityRef, string.Format(this.GetString(Resource.String.setting_priv_invites),newstate), ToastLength.Short).Show();
                ReconfigureOptionsAPI(e.IsChecked, null, null);

            }
        }

        private void ReconfigureOptionsAPI(bool? allowPrivateInvites, bool? enableListener, int? newPort)
        {
            bool requiresConnection = allowPrivateInvites.HasValue;
            if (!SoulSeekState.currentlyLoggedIn && requiresConnection) //note: you CAN in fact change listening and port without being logged in...
            {
                Toast.MakeText(SoulSeekState.MainActivityRef, Resource.String.must_be_logged_to_toggle_priv_invites, ToastLength.Short).Show();
                return;
            }
            if (MainActivity.CurrentlyLoggedInButDisconnectedState() && requiresConnection)
            {
                //we disconnected. login then do the rest.
                //this is due to temp lost connection
                Task t;
                if (!MainActivity.ShowMessageAndCreateReconnectTask(SoulSeekState.ActiveActivityRef, out t))
                {
                    return;
                }
                t.ContinueWith(new Action<Task>((Task t) => {
                    if (t.IsFaulted)
                    {
                        SoulSeekState.MainActivityRef.RunOnUiThread(() => { Toast.MakeText(SoulSeekState.MainActivityRef, Resource.String.failed_to_connect, ToastLength.Short).Show(); });
                        return;
                    }
                    SoulSeekState.MainActivityRef.RunOnUiThread(new Action(() => { ReconfigureOptionsLogic(allowPrivateInvites, enableListener, newPort); }));
                }));
            }
            else
            {
                ReconfigureOptionsLogic(allowPrivateInvites, enableListener, newPort);
            }
        }

        private static void ReconfigureOptionsLogic(bool? allowPrivateInvites, bool? enableTheListener, int? listenerPort)
        {
            //Toast.MakeText(this.Context, "Contacting user for directory list. Will appear in browse tab when complete", ToastLength.Short).Show();
            Task<bool> reconfigTask = null;
            try
            {
                Soulseek.SoulseekClientOptionsPatch patch = new Soulseek.SoulseekClientOptionsPatch(acceptPrivateRoomInvitations: allowPrivateInvites, enableListener: enableTheListener, listenPort: listenerPort);

                reconfigTask = SoulSeekState.SoulseekClient.ReconfigureOptionsAsync(patch);
            }
            catch(Exception e)
            {   //this can still happen on ReqFiles_Click.. maybe for the first check we were logged in but for the second we somehow were not..
                MainActivity.LogFirebase("reconfigure options: " + e.Message + e.StackTrace);
                MainActivity.LogDebug("reconfigure options FAILED" + e.Message + e.StackTrace);
                return;
            }
            Action<Task<bool>> continueWithAction = new Action<Task<bool>>((reconfigTask) => {
                SoulSeekState.ActiveActivityRef.RunOnUiThread(() => {
                    if(reconfigTask.IsFaulted)
                    {
                        MainActivity.LogDebug("reconfigure options FAILED");
                        if(allowPrivateInvites.HasValue)
                        {
                            string enabledDisabled = allowPrivateInvites.Value ? SoulSeekState.ActiveActivityRef.GetString(Resource.String.allowed) : SoulSeekState.ActiveActivityRef.GetString(Resource.String.denied);
                            Toast.MakeText(SoulSeekState.ActiveActivityRef, string.Format(SoulSeekState.ActiveActivityRef.GetString(Resource.String.failed_setting_priv_invites),enabledDisabled), ToastLength.Long).Show();
                            if(SoulSeekState.ActiveActivityRef is SettingsActivity settingsActivity)
                            {
                                //set the check to false
                                settingsActivity.allowPrivateRoomInvitations.Checked = SoulSeekState.AllowPrivateRoomInvitations; //old value
                            }
                        }

                        if(enableTheListener.HasValue)
                        {
                            string enabledDisabled = enableTheListener.Value ? SoulSeekState.ActiveActivityRef.GetString(Resource.String.allowed) : SoulSeekState.ActiveActivityRef.GetString(Resource.String.denied);
                            Toast.MakeText(SoulSeekState.ActiveActivityRef, string.Format(SoulSeekState.ActiveActivityRef.GetString(Resource.String.network_error_setting_listener), enabledDisabled), ToastLength.Long).Show();
                        }

                        if(listenerPort.HasValue)
                        {
                            Toast.MakeText(SoulSeekState.ActiveActivityRef, string.Format(SoulSeekState.ActiveActivityRef.GetString(Resource.String.network_error_setting_listener_port),listenerPort.Value), ToastLength.Long).Show();
                        }



                    }
                    else
                    {
                        if(allowPrivateInvites.HasValue)
                        {
                            MainActivity.LogDebug("reconfigure options SUCCESS, restart required? " + reconfigTask.Result);
                            SoulSeekState.AllowPrivateRoomInvitations = allowPrivateInvites.Value;
                            //set shared prefs...
                            lock (MainActivity.SHARED_PREF_LOCK)
                            {
                                var editor = SoulSeekState.ActiveActivityRef.GetSharedPreferences("SoulSeekPrefs", 0).Edit();
                                editor.PutBoolean(SoulSeekState.M_AllowPrivateRooomInvitations, allowPrivateInvites.Value);
                                bool success = editor.Commit();
                            }
                        }

                        //if(listenerPort.HasValue)
                        //{
                        //    SoulSeekState.ActiveActivityRef.RunOnUiThread( () => {
                        //        if (SoulSeekState.ActiveActivityRef is SettingsActivity settingsActivity)
                        //        {
                        //            ViewGroup v = settingsActivity.FindViewById<ViewGroup>(Android.Resource.Id.Content);
                        //            if(v!=null)
                        //            {
                        //                Action<View> restartSnackbarAction = new Action<View>((v) => {
                        //                    MainActivity.LogInfoFirebase("Restart application clicked");
                        //                    SoulSeekState.SoulseekClient.StopListening();
                        //                    //Restart Application
                        //                    Intent mainIntent = AndroidX.Core.Content.IntentCompat.MakeMainSelectorActivity(Intent.ActionMain, Intent.CategoryLauncher);
                        //                    mainIntent.AddFlags(ActivityFlags.NewTask);
                        //                    SoulSeekState.ActiveActivityRef.ApplicationContext.StartActivity(mainIntent);
                        //                    System.Environment.Exit(0); //equivalent to System.Exit(0) in java.
                        //                });

                        //                bool state = SoulSeekState.SoulseekClient.GetListeningState();
                        //                Snackbar sb = Snackbar.Make(v, "Note - changing port may require you to restart app.", Snackbar.LengthLong).SetAction("Restart", restartSnackbarAction).SetActionTextColor(Resource.Color.lightPurpleNotTransparent);
                        //                (sb.View.FindViewById<TextView>(Resource.Id.snackbar_action) as TextView).SetTextColor(Android.Graphics.Color.ParseColor("#BCC1F7"));//AndroidX.Core.Content.ContextCompat.GetColor(this.Context,Resource.Color.lightPurpleNotTransparent));
                        //                sb.Show();
                        //            }
                        //            else
                        //            {
                        //                Toast.MakeText(SoulSeekState.ActiveActivityRef, "Note - changing port may require you to restart app.", ToastLength.Long).Show();
                        //            }
                        //        }
                        //    });

                        //}
                    }
                });
            });
            reconfigTask.ContinueWith(continueWithAction);
        }

        private void MemoryFileDownloadSwitchIcon_Click(object sender, EventArgs e)
        {
            var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
            var diag = builder.SetMessage(Resource.String.memory_file_backed).SetPositiveButton(Resource.String.close, OnCloseClick).Create();
            diag.Show();
        }

        private void MemoryFileDownloadSwitchCheckBox_CheckedChange(object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            SoulSeekState.MemoryBackedDownload = !e.IsChecked;
            SetIncompleteFolderView();
        }

        private void DisableDownloadNotification_CheckedChange(object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            SoulSeekState.DisableDownloadToastNotification = e.IsChecked;
        }

        private void FreeUploadSlotsOnly_CheckedChange(object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            SoulSeekState.FreeUploadSlotsOnly = e.IsChecked;
        }

        private void MoreInfoButton_Click(object sender, EventArgs e)
        {
            var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
            var diag = builder.SetMessage(Resource.String.sharing_dialog).SetPositiveButton(Resource.String.close, OnCloseClick).Create();
            diag.Show();
        }

        private void OnCloseClick(object sender, DialogClickEventArgs e)
        {
            (sender as AndroidX.AppCompat.App.AlertDialog).Dismiss();
        }

        private void UpdateShareImageView()
        {
            if(SoulSeekState.MainActivityRef==null)
            {
                MainActivity.LogFirebase("UpdateShareImageView MainActivityRef==null");//this caused a fatal crash.. back when GetSharingMessageAndIcon() was not static....
            }
            Tuple<SharingIcons,string> info = MainActivity.GetSharingMessageAndIcon(out bool isParsing);
            ImageView imageView = this.FindViewById<ImageView>(Resource.Id.sharedStatus);
            if(imageView==null) return;
            string toolTip = info.Item2;
            int numParsed = SoulSeekState.NumberParsed;
            if (isParsing && numParsed != 0)
            {
                if(numParsed==int.MaxValue) //our signal we are finishing up (i.e. creating token index)
                {
                    toolTip = toolTip + " (finishing up)";
                }
                else
                {
                    toolTip = toolTip + String.Format(" ({0} files parsed)", numParsed);
                }
            }
            if ((int)Android.OS.Build.VERSION.SdkInt >= 26)
            {
                imageView.TooltipText = toolTip; //api26+ otherwise crash...
            }
            else
            {
                AndroidX.AppCompat.Widget.TooltipCompat.SetTooltipText(imageView, toolTip);
            }
            switch(info.Item1)
            {
                case SharingIcons.On:
                    imageView.SetImageResource(Resource.Drawable.ic_file_upload_black_24dp);
                    break;
                case SharingIcons.Error:
                    imageView.SetImageResource(Resource.Drawable.ic_error_outline_white_24dp);
                    break;
                case SharingIcons.Off:
                    imageView.SetImageResource(Resource.Drawable.ic_sharing_off_black_24dp);
                    break;
            }
            
        }

        private void ShareCheckBox_CheckedChange(object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            SoulSeekState.SharingOn = e.IsChecked;
            /*SoulSeekState.MainActivityRef.*/MainActivity.SetUnsetSharingBasedOnConditions(true);
            UpdateShareImageView();
            UpdateSharingViewState();
            SetSharedFolderView();
        }

        private void UpdateSharingViewState()
        {
            //this isnt winforms where disabling parent, disables all children..

            if(SoulSeekState.SharingOn)
            {
                sharingSubLayout1.Enabled = true;
                sharingSubLayout1.Alpha = 1.0f;
                sharingSubLayout2.Enabled = true;
                sharingSubLayout2.Alpha = 1.0f;
                sharedFolderButton.Clickable = true;
                browseSelfButton.Clickable = true;
                rescanSharesButton.Clickable = true;
            }
            else
            {
                sharingSubLayout1.Enabled = false;
                sharingSubLayout1.Alpha = 0.5f;
                sharingSubLayout2.Enabled = false;
                sharingSubLayout2.Alpha = 0.5f;
                sharedFolderButton.Clickable = false;
                browseSelfButton.Clickable = false;
                rescanSharesButton.Clickable = false;
            }
        }

        private void UpdateListeningViewState()
        {
            if(SoulSeekState.ListenerEnabled)
            {
                listeningSubLayout2.Enabled = true;
                listeningSubLayout3.Enabled = true;
                listeningSubLayout2.Alpha = 1.0f;
                listeningSubLayout3.Alpha = 1.0f;
                useUPnPCheckBox.Clickable = true;
                changePort.Clickable = true;
                checkStatus.Clickable = true;
            }
            else
            {
                listeningSubLayout2.Enabled = false;
                listeningSubLayout3.Enabled = false;
                listeningSubLayout2.Alpha = 0.5f;
                listeningSubLayout3.Alpha = 0.5f;
                useUPnPCheckBox.Clickable = false;
                changePort.Clickable = false;
                checkStatus.Clickable = false;
            }
        }

        private void ImageView_Click(object sender, EventArgs e)
        {
            UpdateShareImageView();
            (sender as ImageView).PerformLongClick();
        }

        private void DayNightMode_ItemSelected(object sender, AdapterView.ItemSelectedEventArgs e)
        {

            MainActivity.LogDebug("DayNightMode_ItemSelected: Pos:" + e.Position + "state: " + SoulSeekState.DayNightMode + "actual: " + AppCompatDelegate.DefaultNightMode);

            if(e.Position == 0)
            {
                SoulSeekState.DayNightMode = -1;
            }
            else
            {
                SoulSeekState.DayNightMode = e.Position;
            }
            lock (MainActivity.SHARED_PREF_LOCK)
            {
                var editor = this.GetSharedPreferences("SoulSeekPrefs", 0).Edit();
            editor.PutInt(SoulSeekState.M_DayNightMode, SoulSeekState.DayNightMode);
            bool success = editor.Commit();
            int x = this.GetSharedPreferences("SoulSeekPrefs", 0).GetInt(SoulSeekState.M_DayNightMode,-1);
            MainActivity.LogDebug("was commit successful: "  + success);
            MainActivity.LogDebug("after writing and immediately reading: " + x);
            }
            //auto = 0, light = 1, dark = 2.  //NO we do NOT want to do AUTO, that is follow time.  we want to do FOLLOW SYSTEM i.e. -1.
            switch (e.Position)
            {
                case 0:
                    if(AppCompatDelegate.DefaultNightMode == -1)
                    {
                        return;
                    }
                    else
                    {
                        AppCompatDelegate.DefaultNightMode = (int)(AppCompatDelegate.ModeNightFollowSystem);
                        //this.Recreate();
                    }
                    break;
                case 1:
                    if (AppCompatDelegate.DefaultNightMode == 1)
                    {
                        return;
                    }
                    else
                    {
                        AppCompatDelegate.DefaultNightMode = (int)(AppCompatDelegate.ModeNightNo);
                        //this.Recreate();
                    }
                    break;
                case 2:
                    if (AppCompatDelegate.DefaultNightMode == 2)
                    {
                        return;
                    }
                    else
                    {
                        AppCompatDelegate.DefaultNightMode = (int)(AppCompatDelegate.ModeNightYes);
                        //this.Recreate();
                    }
                    break;
                default:
                    return;
            }
        }

        private void RememberSearchHistory_CheckedChange(object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            SoulSeekState.RememberSearchHistory = e.IsChecked;
        }

        private void ClearHistory_Click(object sender, EventArgs e)
        {
            SoulSeekState.ClearSearchHistoryInvoke();
        }

        private void AutoClearCompleteUploads_CheckedChange(object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            SoulSeekState.AutoClearCompleteUploads = e.IsChecked;
        }

        private void AutoClearComplete_CheckedChange(object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            SoulSeekState.AutoClearCompleteDownloads = e.IsChecked;
        }

        private void SetSpinnerPosition(Spinner s)
        {
            int selectionIndex = 3;
            foreach (var pair in positionNumberPairs)
            {
                if (pair.Item2 == SoulSeekState.NumberSearchResults)
                {
                    selectionIndex = pair.Item1;
                }
            }
            s.SetSelection(selectionIndex);
        }

        private void SetSpinnerPositionDayNight(Spinner s)
        {
            s.SetSelection(Math.Max(SoulSeekState.DayNightMode,0)); //-1 -> 0
        }

        private void CloseButton_Click(object sender, EventArgs e)
        {
            this.Finish();
        }

        private void RestoreDefaults_Click(object sender, EventArgs e)
        {
            SoulSeekState.NumberSearchResults = MainActivity.DEFAULT_SEARCH_RESULTS;
            SoulSeekState.AutoClearCompleteDownloads = false;
            SoulSeekState.AutoClearCompleteUploads = false;
            SoulSeekState.RememberSearchHistory = true;
            SoulSeekState.ShowRecentUsers = true;
            SoulSeekState.SharingOn = false;
            SoulSeekState.FreeUploadSlotsOnly = true;
            SoulSeekState.DisableDownloadToastNotification = false;
            SoulSeekState.MemoryBackedDownload = false;
            SoulSeekState.DayNightMode = AppCompatDelegate.ModeNightFollowSystem;
            (FindViewById<CheckBox>(Resource.Id.autoClearComplete) as CheckBox).Checked = SoulSeekState.AutoClearCompleteDownloads;
            (FindViewById<CheckBox>(Resource.Id.autoClearCompleteUploads) as CheckBox).Checked = SoulSeekState.AutoClearCompleteUploads;
            (FindViewById<CheckBox>(Resource.Id.searchHistoryRemember) as CheckBox).Checked = SoulSeekState.RememberSearchHistory;
            (FindViewById<CheckBox>(Resource.Id.rememberRecentUsers) as CheckBox).Checked = SoulSeekState.ShowRecentUsers;
            (FindViewById<CheckBox>(Resource.Id.enableSharing) as CheckBox).Checked = SoulSeekState.SharingOn;
            (FindViewById<CheckBox>(Resource.Id.freeUploadSlots) as CheckBox).Checked = SoulSeekState.FreeUploadSlotsOnly;
            (FindViewById<CheckBox>(Resource.Id.disableToastNotificationOnDownload) as CheckBox).Checked = SoulSeekState.DisableDownloadToastNotification;
            (FindViewById<CheckBox>(Resource.Id.memoryFileDownloadSwitchCheckBox) as CheckBox).Checked = !SoulSeekState.MemoryBackedDownload;
            Spinner searchNumSpinner = FindViewById<Spinner>(Resource.Id.searchNumberSpinner);
            SetSpinnerPosition(searchNumSpinner);
            Spinner daynightSpinner = FindViewById<Spinner>(Resource.Id.nightModeSpinner);
            SetSpinnerPositionDayNight(daynightSpinner);
        }

        private void SearchNumSpinner_ItemSelected(object sender, AdapterView.ItemSelectedEventArgs e)
        {
            SoulSeekState.NumberSearchResults = positionNumberPairs[e.Position].Item2;
        }

        private void ChangeDownloadDirectory(object sender, EventArgs e)
        {
            ShowDirSettings(SoulSeekState.SaveDataDirectoryUri,DirectoryType.Download);
        }

        private void ChangeUploadDirectory(object sender, EventArgs e)
        {
            if (Android.Support.V4.Content.ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.ReadExternalStorage) == Android.Content.PM.Permission.Denied) //you dont have this on api >= 29 because you never requested it, but it is NECESSARY to read media store
            {
                Android.Support.V4.App.ActivityCompat.RequestPermissions(this, new string[] { Android.Manifest.Permission.ReadExternalStorage }, READ_EXTERNAL_FOR_MEDIA_STORE);
            }
            else
            {
                ShowDirSettings(SoulSeekState.UploadDataDirectoryUri, DirectoryType.Upload);
            }
        }

        private void ChangeIncompleteDirectory(object sender, EventArgs e)
        {
            ShowDirSettings(SoulSeekState.ManualIncompleteDataDirectoryUri, DirectoryType.Incomplete);
        }

        private void UseApiBelow21Method(int requestCode)
        {
            //Create FolderOpenDialog
            SimpleFileDialog fileDialog = new SimpleFileDialog(this, SimpleFileDialog.FileSelectionMode.FolderChoose);
            fileDialog.GetFileOrDirectoryAsync(Android.OS.Environment.ExternalStorageDirectory.AbsolutePath).ContinueWith(
                (Task<string> t) => {
                    if (t.Result == null || t.Result == string.Empty)
                    {
                        return;
                    }
                    else
                    {
                        Android.Net.Uri uri = Android.Net.Uri.FromFile(new Java.IO.File(t.Result));
                        DocumentFile f = DocumentFile.FromFile(new Java.IO.File(t.Result)); //from tree uri not added til 21 also.  from single uri returns a f.Exists=false file.
                        if(f==null)
                        {
                            MainActivity.LogFirebase("api<21 f is null");
                            SoulSeekState.ActiveActivityRef.RunOnUiThread(() => { Toast.MakeText(this, Resource.String.error_reading_dir, ToastLength.Long).Show(); });
                            return;
                        }
                        else if(!f.Exists())
                        {
                            MainActivity.LogFirebase("api<21 f does not exist");
                            SoulSeekState.ActiveActivityRef.RunOnUiThread(() => { Toast.MakeText(this, Resource.String.error_reading_dir, ToastLength.Long).Show(); });
                            return;
                        }
                        else if(!f.IsDirectory)
                        {
                            MainActivity.LogFirebase("api<21 NOT A DIRECTORY");
                            SoulSeekState.ActiveActivityRef.RunOnUiThread(() => {Toast.MakeText(this, Resource.String.error_not_a_dir, ToastLength.Long).Show(); });
                            return;
                        }

                        if (requestCode == CHANGE_WRITE_EXTERNAL_LEGACY)
                        {
                            this.SuccessfulWriteExternalLegacyCallback(uri, true);
                        }
                        else if(requestCode == UPLOAD_DIR_CHANGE_WRITE_EXTERNAL_LEGACY)
                        {
                            this.SuccessfulUploadExternalCallback(uri, requestCode, true);
                        }
                        else if(requestCode == CHANGE_INCOMPLETE_EXTERNAL_LEGACY)
                        {
                            this.SuccessfulIncompleteExternalLegacyCallback(uri, true);
                        }
                    }


                });
        }



        private void ShowDirSettings(string startingDirectory, DirectoryType directoryType)
        {
            int requestCode = -1;
            if (SoulSeekState.UseLegacyStorage())
            {
                var legacyIntent = new Intent(Intent.ActionOpenDocumentTree);
                if (startingDirectory != null && startingDirectory != string.Empty)
                {
                    Android.Net.Uri res = Android.Net.Uri.Parse(startingDirectory);
                    legacyIntent.PutExtra(DocumentsContract.ExtraInitialUri, res);
                }
                legacyIntent.AddFlags(ActivityFlags.GrantPersistableUriPermission | ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission | ActivityFlags.GrantPrefixUriPermission);
                if (directoryType == DirectoryType.Download)
                {
                    requestCode = CHANGE_WRITE_EXTERNAL_LEGACY;
                }
                else if (directoryType == DirectoryType.Upload)
                {
                    requestCode = UPLOAD_DIR_CHANGE_WRITE_EXTERNAL_LEGACY;
                }
                else if (directoryType == DirectoryType.Incomplete)
                {
                    requestCode = CHANGE_INCOMPLETE_EXTERNAL_LEGACY;
                }
                try
                {
                    this.StartActivityForResult(legacyIntent, requestCode);
                }
                catch (Exception e)
                {
                    if (e.Message.ToLower().Contains("no activity found to handle"))
                    {
                        if(Android.OS.Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
                        {
                            Toast.MakeText(SoulSeekState.ActiveActivityRef, Resource.String.error_no_file_manager, ToastLength.Long).Show();
                        }
                        else //API 19 and 20 will always fail with that error message... therefore use our internal method...
                        {
                            UseApiBelow21Method(requestCode);
                        }

                    }
                    else
                    {
                        MainActivity.LogFirebase("showDirSettings: " + e.Message + e.StackTrace);
                    }
                }
        }
            else
            {
                var storageManager = Android.OS.Storage.StorageManager.FromContext(this);
                var intent = storageManager.PrimaryStorageVolume.CreateOpenDocumentTreeIntent();
                intent.AddFlags(ActivityFlags.GrantPersistableUriPermission | ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission | ActivityFlags.GrantPrefixUriPermission);
                if (startingDirectory != null && startingDirectory != string.Empty)
                {
                    Android.Net.Uri res = Android.Net.Uri.Parse(startingDirectory);
                    intent.PutExtra(DocumentsContract.ExtraInitialUri, res);
                }
                if (directoryType == DirectoryType.Download)
                {
                    requestCode = CHANGE_WRITE_EXTERNAL;
                }
                else if(directoryType == DirectoryType.Upload)
                {
                    requestCode = UPLOAD_DIR_CHANGE_WRITE_EXTERNAL;
                }
                else if(directoryType == DirectoryType.Incomplete)
                {
                    requestCode = CHANGE_INCOMPLETE_EXTERNAL;
                }
                try
                {
                    this.StartActivityForResult(intent, requestCode);
                }
                catch(Exception e)
                {
                    if(e.Message.ToLower().Contains("no activity found to handle"))
                    {
                        Toast.MakeText(SoulSeekState.ActiveActivityRef, Resource.String.error_no_file_manager, ToastLength.Long).Show();
                    }
                    else
                    {
                        MainActivity.LogFirebase("showDirSettings: " + e.Message + e.StackTrace);
                    }
                }
            }
        }

        private void SuccessfulWriteExternalLegacyCallback(Android.Net.Uri uri, bool fromSubApi21=false)
        {
            var x = uri;
            //SoulSeekState.RootDocumentFile = DocumentFile.FromTreeUri(this, data.Data);
            SoulSeekState.SaveDataDirectoryUri = uri.ToString();
            //this.ContentResolver.TakePersistableUriPermission(data.Data, ActivityFlags.GrantWriteUriPermission);
            DocumentFile docFile = null;
            if (fromSubApi21)
            {
                docFile = DocumentFile.FromFile(new Java.IO.File(uri.Path));
            }
            else
            {
                docFile = DocumentFile.FromTreeUri(this, uri);
            }
            SoulSeekState.RootDocumentFile = docFile; 
            this.RunOnUiThread(new Action(() =>
            {
                SoulSeekState.DirectoryUpdatedEvent?.Invoke(null, new EventArgs());
                Toast.MakeText(this, string.Format(this.GetString(Resource.String.successfully_changed_dl_dir), uri.Path), ToastLength.Long).Show();
            }));
        }

        public static bool UseTempDirectory()
        {
            return !UseIncompleteManualFolder() && !SoulSeekState.CreateCompleteAndIncompleteFolders;
        }

        private void SuccessfulIncompleteExternalLegacyCallback(Android.Net.Uri uri, bool fromSubApi21 = false)
        {
            var x = uri;
            //SoulSeekState.RootDocumentFile = DocumentFile.FromTreeUri(this, data.Data);
            SoulSeekState.ManualIncompleteDataDirectoryUri = uri.ToString();
            //this.ContentResolver.TakePersistableUriPermission(data.Data, ActivityFlags.GrantWriteUriPermission);
            DocumentFile docFile = null;
            if (fromSubApi21)
            {
                docFile = DocumentFile.FromFile(new Java.IO.File(uri.Path));
            }
            else
            {
                docFile = DocumentFile.FromTreeUri(this, uri);
            }
            SoulSeekState.RootIncompleteDocumentFile = docFile;
            this.RunOnUiThread(new Action(() =>
            {
                SoulSeekState.DirectoryUpdatedEvent?.Invoke(null, new EventArgs());
                Toast.MakeText(this, string.Format(this.GetString(Resource.String.successfully_changed_incomplete_dir), uri.Path), ToastLength.Long).Show();
            }));
        }


        private void SuccessfulUploadExternalCallback(Android.Net.Uri uri, int requestCode, bool fromSubApi21 = false, bool rescan = false)
        {
            Action parseDatabaseAndUpdateUI = new Action(() => {
                try
                {
                    int prevFiles = -1;
                    bool success = false;
                    SoulSeekState.IsParsing = true;
                    if(rescan && SoulSeekState.SharedFileCache != null)
                    {
                        prevFiles = SoulSeekState.SharedFileCache.FileCount;
                    }
                    this.RunOnUiThread(new Action(() =>
                    {
                        UpdateShareImageView(); //for is parsing..
                        SetSharedFolderView();
                    }));
                    try
                    {
                        SoulSeekState.UploadDataDirectoryUri = uri.ToString();
                        success = SoulSeekState.MainActivityRef.InitializeDatabase(fromSubApi21 ? DocumentFile.FromFile(new Java.IO.File(uri.Path)) : DocumentFile.FromTreeUri(this, uri), false, rescan, out string errorMessage);
                        if(!success)
                        {
                            throw new Exception("Failed to parse shared files: " + errorMessage);
                        }
                        SoulSeekState.IsParsing = false;
                    }
                    catch (Exception e)
                    {
                        SoulSeekState.IsParsing = false;
                        SoulSeekState.UploadDataDirectoryUri = null;
                        SoulSeekState.MainActivityRef.ClearParsedCacheResults();
                        MainActivity.SetUnsetSharingBasedOnConditions(true);
                        MainActivity.LogFirebase("error parsing: " + e.Message + "  " + e.StackTrace);
                        this.RunOnUiThread(new Action(() =>
                        {
                            UpdateShareImageView();
                            SetSharedFolderView();
                            Toast.MakeText(this, e.Message, ToastLength.Long).Show();
                        }));
                        return;
                    }
                    SoulSeekState.UploadDataDirectoryUri = uri.ToString();
                    if (UPLOAD_DIR_CHANGE_WRITE_EXTERNAL == requestCode && !rescan)
                    {
                        this.ContentResolver.TakePersistableUriPermission(uri, ActivityFlags.GrantWriteUriPermission | ActivityFlags.GrantReadUriPermission);
                    }
                    //setup soulseek client with handlers if all conditions met
                    MainActivity.SetUnsetSharingBasedOnConditions(true, true);
                    this.RunOnUiThread(new Action(() =>
                    {
                        UpdateShareImageView();
                        SetSharedFolderView();
                        int dirs = SoulSeekState.SharedFileCache.DirectoryCount; //TODO: nullref here... U318AA, LG G7 ThinQ, both android 10
                        int files = SoulSeekState.SharedFileCache.FileCount;
                        string msg = string.Format(this.GetString(Resource.String.success_setting_shared_dir_fnum_dnum), dirs, files);
                        if(rescan) //tack on additional message if applicable..
                        {
                            int diff = files - prevFiles;
                            if(diff > 0)
                            {
                                if(diff > 1)
                                {
                                    msg = msg + String.Format(" {0} additional files since last scan.",diff);
                                }
                                else
                                {
                                    msg = msg + " 1 additional file since last scan.";
                                }
                            }
                        }
                        Toast.MakeText(this, msg, ToastLength.Long).Show();
                    }));
                }
                finally
                {
                    SoulSeekState.IsParsing = false;
                }
            });
            System.Threading.ThreadPool.QueueUserWorkItem((object o) => { parseDatabaseAndUpdateUI(); });
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            if (READ_EXTERNAL_FOR_MEDIA_STORE == requestCode)
            {
                if (grantResults.Length > 0 && grantResults[0] == Permission.Granted)
                {
                    ShowDirSettings(SoulSeekState.UploadDataDirectoryUri, DirectoryType.Upload);
                }
            }
        }

        protected override void OnActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);
            if (CHANGE_WRITE_EXTERNAL == requestCode)
            {
                if(resultCode == Result.Ok)
                {
                    var x = data.Data;
                    SoulSeekState.RootDocumentFile = DocumentFile.FromTreeUri(this, data.Data);
                    SoulSeekState.SaveDataDirectoryUri = data.Data.ToString();
                    this.ContentResolver.TakePersistableUriPermission(data.Data, ActivityFlags.GrantWriteUriPermission| ActivityFlags.GrantReadUriPermission);
                    this.RunOnUiThread(new Action( ()=>
                    {
                        SoulSeekState.DirectoryUpdatedEvent?.Invoke(null, new EventArgs());
                        Toast.MakeText(this, string.Format(this.GetString(Resource.String.successfully_changed_dl_dir),data.Data), ToastLength.Long).Show();
                    }));
                }
            }
            if(CHANGE_WRITE_EXTERNAL_LEGACY == requestCode)
            {
                if (resultCode == Result.Ok)
                {
                    this.ContentResolver.TakePersistableUriPermission(data.Data, ActivityFlags.GrantWriteUriPermission | ActivityFlags.GrantReadUriPermission);
                    SuccessfulWriteExternalLegacyCallback(data.Data);
                }
            }


            if (CHANGE_INCOMPLETE_EXTERNAL == requestCode)
            {
                if (resultCode == Result.Ok)
                {
                    var x = data.Data;
                    SoulSeekState.RootIncompleteDocumentFile = DocumentFile.FromTreeUri(this, data.Data);
                    SoulSeekState.ManualIncompleteDataDirectoryUri = data.Data.ToString();
                    this.ContentResolver.TakePersistableUriPermission(data.Data, ActivityFlags.GrantWriteUriPermission | ActivityFlags.GrantReadUriPermission);
                    this.RunOnUiThread(new Action(() =>
                    {
                        SoulSeekState.DirectoryUpdatedEvent?.Invoke(null, new EventArgs());
                        Toast.MakeText(this, string.Format(this.GetString(Resource.String.successfully_changed_incomplete_dir), data.Data), ToastLength.Long).Show();
                    }));
                }
            }
            if (CHANGE_INCOMPLETE_EXTERNAL_LEGACY == requestCode)
            {
                if (resultCode == Result.Ok)
                {
                    this.ContentResolver.TakePersistableUriPermission(data.Data, ActivityFlags.GrantWriteUriPermission | ActivityFlags.GrantReadUriPermission);
                    SuccessfulIncompleteExternalLegacyCallback(data.Data);
                }
            }


            if (UPLOAD_DIR_CHANGE_WRITE_EXTERNAL == requestCode || UPLOAD_DIR_CHANGE_WRITE_EXTERNAL_LEGACY==requestCode)
            {
                if(resultCode != Result.Ok)
                {
                    return;
                }

                //make sure you can parse the files before setting the directory..

                //this takes 5+ seconds in Debug mode (with 20-30 albums) which means that this MUST be done on a separate thread..
                Toast.MakeText(this, Resource.String.parsing_files_wait, ToastLength.Long).Show();

                SuccessfulUploadExternalCallback(data.Data, requestCode, false);

            }


        }

        public static void RestoreAdditionalDirectorySettingsFromSharedPreferences()
        {
            lock (MainActivity.SHARED_PREF_LOCK)
            {
                SoulSeekState.CreateCompleteAndIncompleteFolders = SoulSeekState.SharedPreferences.GetBoolean(SoulSeekState.M_CreateCompleteAndIncompleteFolders, true);
                SoulSeekState.OverrideDefaultIncompleteLocations = SoulSeekState.SharedPreferences.GetBoolean(SoulSeekState.M_UseManualIncompleteDirectoryUri, false);
                SoulSeekState.CreateUsernameSubfolders = SoulSeekState.SharedPreferences.GetBoolean(SoulSeekState.M_AdditionalUsernameSubdirectories, false);
                SoulSeekState.ManualIncompleteDataDirectoryUri = SoulSeekState.SharedPreferences.GetString(SoulSeekState.M_ManualIncompleteDirectoryUri, string.Empty);
            }
        }

        public static void SaveAdditionalDirectorySettingsToSharedPreferences()
        {
            lock (MainActivity.SHARED_PREF_LOCK)
            {
                var editor = SoulSeekState.SharedPreferences.Edit();
                editor.PutBoolean(SoulSeekState.M_CreateCompleteAndIncompleteFolders, SoulSeekState.CreateCompleteAndIncompleteFolders);
                editor.PutBoolean(SoulSeekState.M_UseManualIncompleteDirectoryUri, SoulSeekState.OverrideDefaultIncompleteLocations);
                editor.PutBoolean(SoulSeekState.M_AdditionalUsernameSubdirectories, SoulSeekState.CreateUsernameSubfolders);
                editor.PutString(SoulSeekState.M_ManualIncompleteDirectoryUri, SoulSeekState.ManualIncompleteDataDirectoryUri);
                bool success = editor.Commit();
            }
        }

        public static void SaveManualIncompleteDirToSharedPreferences()
        {
            lock (MainActivity.SHARED_PREF_LOCK)
            {
                var editor = SoulSeekState.SharedPreferences.Edit();
                editor.PutString(SoulSeekState.M_ManualIncompleteDirectoryUri, SoulSeekState.ManualIncompleteDataDirectoryUri);
                bool success = editor.Commit();
            }
        }

    }

    enum DirectoryType : ushort
    {
        Download = 0,
        Upload = 1,
        Incomplete = 2
    }
}

