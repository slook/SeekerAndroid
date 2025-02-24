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

using Android.Content;
using Android.OS;
using Android.Support.Design.Widget;
using Android.Support.V4.App;
using Android.Text;
using Android.Util;
using Android.Views;
using Android.Widget;
using Google.Android.Material.Snackbar;
using Soulseek;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AndroidX.RecyclerView.Widget;

namespace AndriodApp1
{
    public class BrowseFragment : Fragment
    {
        //for filtering - we always just get the filtered copy from the main copy on the fly.
        //the main copy will move up, down, etc.  so no need for the filtered copy to keep track of any of that
        //just do what we normally do and then generate the filtered copy as the very last step

        //private static IParcelable listViewState = null; restoring this did not restore scroll pos
        public View rootView;
        
        private ListView listViewDirectories;
        private RecyclerView treePathRecyclerView;
        private LinearLayoutManager treePathLayoutManager;
        private TreePathRecyclerAdapter treePathRecyclerAdapter;

        private static List<DataItem> dataItemsForListView = new List<DataItem>(); 
        private static List<PathItem> pathItems = new List<PathItem>(); 
        public static string CurrentUsername;
        private static Tuple<string,List<DataItem>> cachedFilteredDataItemsForListView = null;//to help with superSetQueries new Tuple<string, List<DataItem>>; 
        private static int diagnostics_count;
        private static List<DataItem> filteredDataItemsForListView = new List<DataItem>(); 
        private static bool refreshOnCreate = false;
        private bool tempHackItemClick = false;
        private static string username = "";
        private bool isPaused = true;
        private View noBrowseView = null;
        private View separator = null;
        public static Stack<Tuple<int,int>> ScrollPositionRestore = new Stack<Tuple<int, int>>(); //indexOfItem, topmargin. for going up/down dirs.
        public static Tuple<int,int> ScrollPositionRestoreRotate = null; //for rotating..

        public static bool FilteredResults = false;
        public static string FilterString = string.Empty;
        public static List<string> WordsToAvoid = new List<string>();
        public static List<string> WordsToInclude = new List<string>();
        public static List<int> SelectedPositionsState = new List<int>(); //this is used for restoring our state.  if its an empty list then thats fine, its just like if we didnt have one..
        public static System.Timers.Timer DebounceTimer = null;
        public static System.Diagnostics.Stopwatch DiagStopWatch = new System.Diagnostics.Stopwatch();
        public static long lastTime = -1;

        public BrowseFragment() : base()
        {
            if(DebounceTimer==null)
            {
                DebounceTimer = new System.Timers.Timer(250);
                
                DebounceTimer.AutoReset = false;
            }
            DiagStopWatch.Start();
        }

        /// <summary>
        /// For use going down directories
        /// </summary>
        private void SaveScrollPosition()
        {
            try
            {
                int index = listViewDirectories.FirstVisiblePosition;
                View v = listViewDirectories.GetChildAt(0);
                int top = (v == null) ? 0 : (v.Top - listViewDirectories.PaddingTop);
                ScrollPositionRestore.Push(new Tuple<int,int>(index,top));
            }
            catch(Exception e)
            {
                MainActivity.LogFirebase(e.Message + e.StackTrace);
            }
        }

        /// <summary>
        /// For use rotating screen, leaving and coming back, etc...
        /// </summary>
        private void SaveScrollPositionRotate()
        {
            try
            {
                int index = listViewDirectories.FirstVisiblePosition;
                View v = listViewDirectories.GetChildAt(0);
                int top = (v == null) ? 0 : (v.Top - listViewDirectories.PaddingTop);
                ScrollPositionRestoreRotate = new Tuple<int, int>(index, top);
            }
            catch (Exception e)
            {
                MainActivity.LogFirebase(e.Message + e.StackTrace);
            }
        }

        /// <summary>
        /// For use going up directories
        /// </summary>
        private void RestoreScrollPosition()
        {
            try
            {
                Tuple<int,int> pos = ScrollPositionRestore.Pop();
                listViewDirectories.SetSelectionFromTop(pos.Item1,pos.Item2);
            }
            catch (Exception e)
            {
                MainActivity.LogFirebase(e.Message + e.StackTrace);
            }

        }

        private void DebounceTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            UpdateFilteredResponses(); // this is the expensive function...
            SoulSeekState.MainActivityRef.RunOnUiThread(() => {
                lock(filteredDataItemsForListView)
                {
                    BrowseAdapter customAdapter = new BrowseAdapter(SoulSeekState.MainActivityRef, filteredDataItemsForListView,this); //enumeration exception (that is, before I added the lock)
                    ListView lv = rootView?.FindViewById<ListView>(Resource.Id.listViewDirectories);
                    if(lv!=null)
                    {
                        lv.Adapter = (customAdapter);
                    }
                }
            });
        }

        public override void OnCreateOptionsMenu(IMenu menu, MenuInflater inflater)
        {
            if(IsResponseLoaded())
            {
                inflater.Inflate(Resource.Menu.browse_menu_full, menu);
            }
            else
            {
                inflater.Inflate(Resource.Menu.browse_menu_empty, menu);
            }
            base.OnCreateOptionsMenu(menu, inflater);
        }

        public override void OnPrepareOptionsMenu(IMenu menu)
        {
            Helpers.SetMenuTitles(menu,username);
            base.OnPrepareOptionsMenu(menu);
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            if(item.ItemId != Resource.Id.action_browse_user) //special handling (this browse user means browse user dialog).
            {
                if(Helpers.HandleCommonContextMenuActions(item.TitleFormatted.ToString(),username,SoulSeekState.ActiveActivityRef, null))
                {
                    return true;
                }
            }
            switch (item.ItemId)
            {
                case Resource.Id.action_browse_user:
                    ShowEditTextBrowseUserDialog();
                    return true;
                case Resource.Id.action_up_directory:
                    GoUpDirectory();
                    return true;
                case Resource.Id.action_download_files:
                    DownloadUserFilesEntry();
                    return true;
                case Resource.Id.action_download_selected_files:
                    DownloadSelectedFiles();
                    (listViewDirectories.Adapter as BrowseAdapter).SelectedPositions.Clear();
                    ClearAllSelectedPositions();
                    return true;
                case Resource.Id.action_add_user:
                    UserListActivity.AddUserAPI(SoulSeekState.MainActivityRef, username, null);
                    return true;
                case Resource.Id.action_get_user_info:
                    RequestedUserInfoHelper.RequestUserInfoApi(username);
                    return true;
            }
            return base.OnOptionsItemSelected(item);
        }

        public override void SetMenuVisibility(bool menuVisible)
        {
            //this is necessary if programmatically moving to a tab from another activity..
            if (menuVisible)
            {
                var navigator = SoulSeekState.MainActivityRef?.FindViewById<BottomNavigationView>(Resource.Id.navigation);
                if (navigator != null)
                {
                    navigator.Menu.GetItem(3).SetCheckable(true);
                    navigator.Menu.GetItem(3).SetChecked(true);
                }
            }
            base.SetMenuVisibility(menuVisible);
        }

        public override void OnDestroyView()
        {
            DebounceTimer.Elapsed -= DebounceTimer_Elapsed; //the timer is static...
            base.OnDestroyView();
        }

        //public override void OnDestroy() //this never gets called.
        //{
        //    DebounceTimer.Elapsed -= DebounceTimer_Elapsed; 
        //    base.OnDestroy();
        //}

        /// <summary>
        /// This is used to determine whether we should show the "No browse, to get started" message and whether we should use the browse full or empty.  
        /// I changed it from dataItems!=0 because its too confusing if you browse someone who is sharing an empty directory.
        /// </summary>
        /// <returns></returns>
        public bool IsResponseLoaded()
        {
            return !string.IsNullOrEmpty(username); //(dataItemsForListView.Count != 0);
        }
        public static BrowseFragment Instance = null;
        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            Instance = this;
            this.HasOptionsMenu = true;
            SoulSeekState.InDarkModeCache = DownloadDialog.InNightMode(this.Context);
            MainActivity.LogDebug("BrowseFragmentOnCreateView");
            this.rootView = inflater.Inflate(Resource.Layout.browse, container, false);
            UpdateForScreenSize();
            //this.rootView.FindViewById<Button>(Resource.Id.button2).Click += UpDirectory;
            //this.rootView.FindViewById<Button>(Resource.Id.dlFiles).Click += BrowseFragment_Click;
            //Java.Lang.IllegalStateException: 'The specified child already has a parent. You must call removeView() on the child's parent first.' if third param is not false above...
            //if(!refreshOnCreate)
            //{
            listViewDirectories = this.rootView.FindViewById<ListView>(Resource.Id.listViewDirectories);
            listViewDirectories.ItemClick -= ListViewDirectories_ItemClick; //there may be a change of this not getting attached which would be bad
            listViewDirectories.ItemClick += ListViewDirectories_ItemClick; //there may be a change of this not getting attached which would be bad
            DebounceTimer.Elapsed += DebounceTimer_Elapsed;

            treePathRecyclerView = this.rootView.FindViewById<RecyclerView>(Resource.Id.recyclerViewHorizontalPath);
            treePathLayoutManager = new LinearLayoutManager(this.Context, LinearLayoutManager.Horizontal, false);
            treePathRecyclerView.SetLayoutManager(treePathLayoutManager);


            //savedInstanceState can be null if first time.
            int[]? selectedPos = savedInstanceState?.GetIntArray("selectedPositions");
            
            if (FilteredResults)
            {
            //tempHackItemClick = true;
                lock (filteredDataItemsForListView)
                { //on ui thread.
                    currentUsernameUI = CurrentUsername;
                    listViewDirectories.Adapter = new BrowseAdapter(this.Context, filteredDataItemsForListView, this, selectedPos);
                }
            }
            else
            {
                //tempHackItemClick = true;
                lock (dataItemsForListView)
                { //on ui thread.
                    currentUsernameUI = CurrentUsername;
                    listViewDirectories.Adapter = new BrowseAdapter(this.Context, dataItemsForListView, this, selectedPos);
                }
            }

            if(dataItemsForListView.Count!=0)
            {
                pathItems = GetPathItems(dataItemsForListView);
            }

            treePathRecyclerAdapter = new TreePathRecyclerAdapter(pathItems, this);
            treePathRecyclerView.SetAdapter(treePathRecyclerAdapter);

            //}
            this.noBrowseView = this.rootView.FindViewById<TextView>(Resource.Id.noBrowseView);
            this.separator = this.rootView.FindViewById<View>(Resource.Id.recyclerViewHorizontalPathSep);
            this.separator.Visibility = ViewStates.Gone;
            if (FilteredResults || IsResponseLoaded()) // if we are filtering then we already know how it works..
            {
                noBrowseView.Visibility = ViewStates.Gone;
                separator.Visibility = ViewStates.Visible;
            }


            View v = rootView.FindViewById<View>(Resource.Id.relativeLayout1);
            v.Focusable = true;
            //SetFocusable(int) was added in API26. bool was there since API1
            if ((int)Android.OS.Build.VERSION.SdkInt >= 26)
            {
                v.SetFocusable(ViewFocusability.Focusable);
            }
            else
            {
                //v.SetFocusable(true); no bool method in xamarin...
            }

            v.FocusableInTouchMode = true;


            EditText filterText = rootView.FindViewById<EditText>(Resource.Id.filterText);
            filterText.TextChanged += FilterText_TextChanged;
            filterText.FocusChange += FilterText_FocusChange;
            filterText.EditorAction += FilterText_EditorAction;

            Button clearFilter = rootView.FindViewById<Button>(Resource.Id.clearFilter);
            clearFilter.Click += ClearFilter_Click;

            RelativeLayout rel = rootView.FindViewById<RelativeLayout>(Resource.Id.bottomSheet);
            BottomSheetBehavior bsb = BottomSheetBehavior.From(rel);
            bsb.Hideable = true;
            bsb.PeekHeight = 320;
            bsb.State = BottomSheetBehavior.StateHidden;
            View b = rootView.FindViewById<View>(Resource.Id.bsbutton);
            (b as FloatingActionButton).SetImageResource(Resource.Drawable.ic_filter_list_white_24dp);
            b.Click += B_Click;


            return this.rootView;
        }

        private void FilterText_FocusChange(object sender, View.FocusChangeEventArgs e)
        {
            try
            {
                SoulSeekState.MainActivityRef.Window.SetSoftInputMode(SoftInput.AdjustResize);
            }
            catch (System.Exception err)
            {
                MainActivity.LogFirebase("MainActivity_FocusChange" + err.Message);
            }
        }

        public override void OnViewCreated(View view, Bundle savedInstanceState)
        {
            base.OnViewCreated(view, savedInstanceState);

        }



        private void B_Click(object sender, EventArgs e)
        {
            RelativeLayout rel = rootView.FindViewById<RelativeLayout>(Resource.Id.bottomSheet);
            BottomSheetBehavior bsb = BottomSheetBehavior.From(rel);
            if (bsb.State != BottomSheetBehavior.StateExpanded && bsb.State != BottomSheetBehavior.StateCollapsed)
            {
                bsb.State = BottomSheetBehavior.StateExpanded;
            }
            else
            {
                //if the keyboard is up and the edittext is in focus then maybe just put the keyboard down
                //else put the bottom sheet down.  
                //so make it two tiered.
                //or maybe just unset the focus...
                EditText test = rootView.FindViewById<EditText>(Resource.Id.filterText);
                //Android.Views.InputMethods.InputMethodManager IMM = context.GetSystemService(Context.InputMethodService) as Android.Views.InputMethods.InputMethodManager;
                //Rect outRect = new Rect();
                //this.rootView.GetWindowVisibleDisplayFrame(outRect);
                //MainActivity.LogDebug("Window Visible Display Frame " + outRect.Height());
                //MainActivity.LogDebug("Actual Height " + this.rootView.Height);
                //Type immType = IMM.GetType();

                //MainActivity.LogDebug("Y Position " + rel.GetY());
                //int[] location = new int[2];
                //rel.GetLocationOnScreen(location);
                //MainActivity.LogDebug("X Pos: " + location[0] + "  Y Pos: " + location[1]);
                //var method = immType.GetProperty("InputMethodWindowVisibleHeight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                //foreach (var prop in immType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                //{
                //    MainActivity.LogDebug(string.Format("Property Name: {0}", prop.Name));
                //}
                //foreach(var meth in immType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                //{
                //    MainActivity.LogDebug(string.Format("Property Name: {0}", meth.Name));
                //}

                MainActivity.LogDebug(this.Resources.Configuration.HardKeyboardHidden.ToString()); //on pixel2 it is YES. on emulator with HW Keyboard = true it is NO

                if (test.IsFocused && (this.Resources.Configuration.HardKeyboardHidden == Android.Content.Res.HardKeyboardHidden.Yes)) //it can still be focused without the keyboard up...
                {
                    try
                    {

                        //SoulSeekState.MainActivityRef.DispatchKeyEvent(new KeyEvent(new KeyEventActions(),Keycode.Enter));
                        Android.Views.InputMethods.InputMethodManager imm = (Android.Views.InputMethods.InputMethodManager)Context.GetSystemService(Context.InputMethodService);
                        imm.HideSoftInputFromWindow(rootView.WindowToken, 0);
                        test.ClearFocus();
                        rootView.FindViewById<View>(Resource.Id.relativeLayout1).RequestFocus();
                    }
                    catch
                    {
                        //not worth throwing over
                    }
                    return;
                }
                else if (test.IsFocused && (this.Resources.Configuration.HardKeyboardHidden == Android.Content.Res.HardKeyboardHidden.No))
                {

                    //we still want to change focus as otherwise one can still type into it...
                    test.ClearFocus();
                    rootView.FindViewById<View>(Resource.Id.relativeLayout1).RequestFocus();
                    bsb.State = BottomSheetBehavior.StateHidden;

                }
                //test.ClearFocus(); //doesnt do anything. //maybe focus the search text.

                bsb.State = BottomSheetBehavior.StateHidden;
            }
        }


        private void FilterText_EditorAction(object sender, TextView.EditorActionEventArgs e)
        {
            if (e.ActionId == Android.Views.InputMethods.ImeAction.Done || //in this case it is Done (blue checkmark)
                e.ActionId == Android.Views.InputMethods.ImeAction.Go ||
                e.ActionId == Android.Views.InputMethods.ImeAction.Next ||
                e.ActionId == Android.Views.InputMethods.ImeAction.Search)
            {
                MainActivity.LogDebug("IME ACTION: " + e.ActionId.ToString());
                rootView.FindViewById<EditText>(Resource.Id.filterText).ClearFocus();
                rootView.FindViewById<View>(Resource.Id.relativeLayout1).RequestFocus();
                //overriding this, the keyboard fails to go down by default for some reason.....
                try
                {
                    Android.Views.InputMethods.InputMethodManager imm = (Android.Views.InputMethods.InputMethodManager)SoulSeekState.MainActivityRef.GetSystemService(Context.InputMethodService);
                    imm.HideSoftInputFromWindow(rootView.WindowToken, 0);
                }
                catch (System.Exception ex)
                {
                    MainActivity.LogFirebase(ex.Message + " error closing keyboard");
                }
            }
        }


        private void ClearFilter_Click(object sender, EventArgs e)
        {
            //CheckBox filterSticky = rootView.FindViewById<CheckBox>(Resource.Id.stickyFilterCheckbox);
            //filterSticky.Checked = false;
            ClearFilterStringAndCached(true);
        }

        public override void OnSaveInstanceState(Bundle outState)
        {
            outState.PutIntArray("selectedPositions",(this.listViewDirectories?.Adapter as BrowseAdapter)?.SelectedPositions?.ToArray());
            base.OnSaveInstanceState(outState);
        }

        

        public override void OnViewStateRestored(Bundle savedInstanceState)
        {
            base.OnViewStateRestored(savedInstanceState);
            //the system by default will set the value of filterText to whatever it was last... but we keep track of it using the static so do that instead....
            //cant do this OnViewCreated since filterText.Text will always be empty...
            DebounceTimer.Stop();
            EditText filterText = rootView.FindViewById<EditText>(Resource.Id.filterText);
            filterText.Text = FilterString;  //this will often be empty (which is good) if we got a new response... otherwise (on screen rotate, it will be the same as it otherwise was).
        }

        private string currentUsernameUI;
        public static EventHandler<EventArgs> BrowseResponseReceivedUI;

        public void BrowseResponseReceivedUI_Handler(object sender, EventArgs args)
        {
            //if the fragment was never created then this.Context will be null
            SoulSeekState.MainActivityRef.RunOnUiThread(() => {
                //Toast.MakeText(SoulSeekState.MainActivityRef, "Browse Response Received", ToastLength.Short).Show();

                lock (dataItemsForListView)
                {
                    if (BrowseFragment.Instance == null || BrowseFragment.Instance.Context == null || BrowseFragment.Instance.rootView == null)
                    {
                        refreshOnCreate = true;
                    }
                    else
                    {
                        BrowseFragment.Instance.RefreshOnRecieved();
                    }
                }
                var pager = (Android.Support.V4.View.ViewPager)SoulSeekState.MainActivityRef?.FindViewById(Resource.Id.pager);
                if (pager!=null && pager.CurrentItem == 3)
                {
                    SoulSeekState.MainActivityRef.SupportActionBar.Title = this.GetString(Resource.String.browse_tab) + ": " + BrowseFragment.CurrentUsername;
                    SoulSeekState.MainActivityRef.InvalidateOptionsMenu();
                }

            });
        }

        public override void OnResume()
        {
            base.OnResume();
            if(SoulSeekState.MainActivityRef?.SupportActionBar?.Title != null && !string.IsNullOrEmpty(CurrentUsername)
                && !SoulSeekState.MainActivityRef.SupportActionBar.Title.EndsWith(": " + CurrentUsername)
                && SoulSeekState.MainActivityRef.OnBrowseTab())
            {
                SoulSeekState.MainActivityRef.SupportActionBar.Title = this.GetString(Resource.String.browse_tab) + ": " + BrowseFragment.CurrentUsername;
            }
            BrowseResponseReceivedUI += BrowseResponseReceivedUI_Handler;
            if (currentUsernameUI != CurrentUsername)
            {
                currentUsernameUI = CurrentUsername;
                BrowseResponseReceivedUI_Handler(null, new EventArgs());
            }
            Instance = this;
            if (listViewDirectories != null && ScrollPositionRestoreRotate != null)
            {
                //restore scroll
                listViewDirectories.SetSelectionFromTop(ScrollPositionRestoreRotate.Item1, ScrollPositionRestoreRotate.Item2);
            }
            isPaused = false;
        }

        public override void OnPause()
        {
            BrowseResponseReceivedUI -= BrowseResponseReceivedUI_Handler;
            SaveScrollPositionRotate();
            base.OnPause();
            isPaused = true;
        }


        private void ParseFilterString()
        {
            List<string> filterStringSplit = FilterString.Split(' ').ToList();
            WordsToAvoid.Clear();
            WordsToInclude.Clear();
            //FilterSpecialFlags.Clear();
            foreach (string word in filterStringSplit)
            {
                //if (word.Contains("mbr:") || word.Contains("minbitrate:"))
                //{
                //    FilterSpecialFlags.ContainsSpecialFlags = true;
                //    try
                //    {
                //        FilterSpecialFlags.MinBitRateKBS = Integer.ParseInt(word.Split(':')[1]);
                //    }
                //    catch (System.Exception)
                //    {

                //    }
                //}
                //else if (word.Contains("mfs:") || word.Contains("minfilesize:"))
                //{
                //    FilterSpecialFlags.ContainsSpecialFlags = true;
                //    try
                //    {
                //        FilterSpecialFlags.MinFileSizeMB = (Integer.ParseInt(word.Split(':')[1]));
                //    }
                //    catch (System.Exception)
                //    {

                //    }
                //}
                //else if (word.Contains("mfif:") || word.Contains("minfilesinfolder:"))
                //{
                //    FilterSpecialFlags.ContainsSpecialFlags = true;
                //    try
                //    {
                //        FilterSpecialFlags.MinFoldersInFile = Integer.ParseInt(word.Split(':')[1]);
                //    }
                //    catch (System.Exception)
                //    {

                //    }
                //}
                //else if (word == "isvbr")
                //{
                //    FilterSpecialFlags.ContainsSpecialFlags = true;
                //    FilterSpecialFlags.IsVBR = true;
                //}
                //else if (word == "iscbr")
                //{
                //    FilterSpecialFlags.ContainsSpecialFlags = true;
                //    FilterSpecialFlags.IsCBR = true;
                //}
                if (word.StartsWith('-'))
                {
                    WordsToAvoid.Add(word);
                }
                else
                {
                    WordsToInclude.Add(word);
                }
            }
        }


        private void FilterText_TextChanged(object sender, TextChangedEventArgs e)
        {
            MainActivity.LogDebug("time between typing: " + (DiagStopWatch.ElapsedMilliseconds - lastTime).ToString());
            lastTime = DiagStopWatch.ElapsedMilliseconds;
            if(e.Text!=null && e.Text.ToString()!=string.Empty && isPaused)
            {
                return;//this is the case where going from search fragment to browse fragment this event gets fired
                //with an old e.text value and so its impossible to autoclear the value.
            }
            MainActivity.LogDebug("Text Changed: " + e.Text);
            if (e.Text != null && e.Text.ToString() != string.Empty)
            {
                FilteredResults = true;
                FilterString = e.Text.ToString();
                ParseFilterString();
                
                DebounceTimer.Stop(); //average time bewteen typing is around 150-250 ms (if you know what you are going to type etc).  backspacing (i.e. holding it down) is about 50 ms.
                DebounceTimer.Start();
                //UpdateFilteredResponses(); // this is the expensive function...
                //BrowseAdapter customAdapter = new BrowseAdapter(SoulSeekState.MainActivityRef, filteredDataItemsForListView);
                //ListView lv = rootView.FindViewById<ListView>(Resource.Id.listViewDirectories);
                //lv.Adapter = (customAdapter);
            }
            else
            {
                DebounceTimer.Stop();
                FilteredResults = false;
                lock(dataItemsForListView) //collection was modified exception here...
                {
                    BrowseAdapter customAdapter = new BrowseAdapter(SoulSeekState.MainActivityRef, dataItemsForListView, this);
                    ListView lv = rootView.FindViewById<ListView>(Resource.Id.listViewDirectories);
                    lv.Adapter = (customAdapter);
                }
            }
        }

        private bool MatchesCriteriaShallow(DataItem di)
        {
            string fullyQualifiedName = string.Empty;
            if (di.File!=null)
            {
                //we are looking at files here...
                fullyQualifiedName = di.Node.Data.Name + di.File.Filename;
            }
            else
            {
                fullyQualifiedName = di.Node.Data.Name;
                //maybe here we should also do children...
            }


            if (WordsToAvoid != null)
            {
                foreach (string avoid in WordsToAvoid)
                {
                    if (fullyQualifiedName.Contains(avoid, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                        //badTerm = true;
                    }
                }
            }
            if (WordsToInclude != null)
            {
                foreach (string include in WordsToInclude)
                {
                    if (!fullyQualifiedName.Contains(include, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }
            return true;
        }



        private bool MatchesCriteriaFull(DataItem di)
        {
            string fullyQualifiedName = string.Empty;
            if (di.File != null)
            {
                //we are looking at files here...
                fullyQualifiedName = di.Node.Data.Name + di.File.Filename;
            }
            else
            {
                fullyQualifiedName = di.Node.Data.Name;
                //maybe here we should also do children...
            }


            if (WordsToAvoid != null)
            {
                foreach (string avoid in WordsToAvoid)
                {
                    if (fullyQualifiedName.Contains(avoid, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                        //badTerm = true;
                    }
                }
            }
            bool includesAll = true;
            if (WordsToInclude != null)
            {
                foreach (string include in WordsToInclude)
                {
                    if (!fullyQualifiedName.Contains(include, StringComparison.OrdinalIgnoreCase))
                    {
                        includesAll = false;
                    }
                }
            }
            if(includesAll)
            {
                return true;
            }
            else
            {
                //search children for a match.. if there are children.. else we are done..
                if(di.Node.Children.Count==0&&(di.Directory==null || di.Directory.Files.Count==0))
                {
                    return false;
                }
                else if(di.File!=null)
                {
                    //then we are at the end
                    return false;
                }
                else
                {
                    if(di.Node.Children.Count!=0)
                    {
                        foreach(TreeNode<Directory> child in di.Node.Children)
                        {
                            if (MatchesCriteriaFull(new DataItem(child.Data,child)))
                            {
                                return true;
                            }
                        }
                    }
                    if(di.File == null && di.Directory!=null && di.Directory.Files.Count != 0)
                    {
                        foreach (File f in di.Directory.Files)
                        {
                            if (MatchesCriteriaFull(new DataItem(f, di.Node)))
                            {
                                return true;
                            }
                        }
                    }
                    return false;
                }
            }
        }

        /// <summary>
        /// Flattens the tree
        /// </summary>
        /// <param name="di"></param>
        /// <returns></returns>
        public static List<FullFileInfo> GetRecursiveFullFileInfo(DataItem di)
        {
            List <FullFileInfo> listOfFiles = new List<FullFileInfo>();
            AddFiles(listOfFiles,di);
            return listOfFiles;
        }

        public static List<FullFileInfo> GetRecursiveFullFileInfo(List<DataItem> di)
        {
            List<FullFileInfo> listOfFiles = new List<FullFileInfo>();
            foreach (var childNode in di)
            {
                AddFiles(listOfFiles, childNode);
            }
            return listOfFiles;
        }

        private static void AddFiles(List<FullFileInfo> fullFileList, DataItem d)
        {
            if(d.File != null)
            {
                FullFileInfo f = new FullFileInfo();
                f.FileName = d.File.Filename;
                f.FullFileName = d.Node.Data.Name + @"\" + d.File.Filename;
                f.Size = d.File.Size;
                fullFileList.Add(f);
                return;
            }
            else
            {
                foreach(Soulseek.File slskFile in d.Directory.Files) //files in dir
                {
                    FullFileInfo f = new FullFileInfo();
                    f.FileName = slskFile.Filename;
                    f.FullFileName = d.Node.Data.Name + @"\" + slskFile.Filename;
                    f.Size = slskFile.Size;
                    fullFileList.Add(f);
                }
                foreach(var childNode in d.Node.Children) //dirs in dir
                {
                    AddFiles(fullFileList, new DataItem(childNode.Data, childNode));
                }
                return;
            }
        }


        private List<DataItem> FilterBrowseList(List<DataItem> unfiltered)
        {
            System.Diagnostics.Stopwatch s = new System.Diagnostics.Stopwatch();
            s.Start();

            List < DataItem > filtered = new List<DataItem>();
            foreach (DataItem di in unfiltered)
            {
                if(MatchesCriteriaFull(di)) //change back to shallow...
                {
                    filtered.Add(di);
                }
            }
            s.Stop();
            MainActivity.LogDebug("total dir in tree: " + diagnostics_count + " total time: " + s.ElapsedMilliseconds);
            return filtered;
        }

        private void UpdateFilteredResponses()
        {
            lock(filteredDataItemsForListView)
            {
                filteredDataItemsForListView.Clear();
                //filteredBrowseTree = DownloadDialog.CreateTree(OriginalBrowseResponse,true,WordsToAvoid,WordsToInclude);
                //string nameToFindInTheFilteredTree = OurCurrentLocation.Data.Name;
                //TreeNode<Directory> item = GetNodeByName(filteredBrowseTree, nameToFindInTheFilteredTree);
                if(cachedFilteredDataItemsForListView!= null && FilterString.Contains(cachedFilteredDataItemsForListView.Item1)) //is less restrictive than the current search)
                {
                    var test = FilterBrowseList(cachedFilteredDataItemsForListView.Item2);
                    filteredDataItemsForListView.AddRange(test);//FilterBrowseList(cachedFilteredDataItemsForListView.Item2);
                }
                else
                {
                    var test = FilterBrowseList(dataItemsForListView);
                    filteredDataItemsForListView.AddRange(test);
                }
                cachedFilteredDataItemsForListView = new Tuple<string, List<DataItem>>(FilterString,filteredDataItemsForListView.ToList());
            }
        }

        private void UpdateForScreenSize()
        {
            if(!SoulSeekState.IsLowDpi())return;
            try
            {
                //this.rootView.FindViewById<TextView>(Resource.Id.browseQueue).SetTextSize(ComplexUnitType.Dip, 8);
                //this.rootView.FindViewById<TextView>(Resource.Id.browseKbs).SetTextSize(ComplexUnitType.Dip, 8);
            }
            catch
            {
                //not worth throwing over
            }
        }

        private void DownloadSelectedFiles()
        {
            if ((!FilteredResults && dataItemsForListView.Count == 0) || (FilteredResults && filteredDataItemsForListView.Count==0))
            {
                Toast.MakeText(this.Context, this.Resources.GetString(Resource.String.nothing_to_download), ToastLength.Long).Show();
            }
            else if((listViewDirectories.Adapter as BrowseAdapter).SelectedPositions.Count == 0)
            {
                Toast.MakeText(this.Context, this.Resources.GetString(Resource.String.nothing_selected), ToastLength.Long).Show();
            }
            else
            {
                List<FullFileInfo> slskFile = new List<FullFileInfo>();
                if (FilteredResults)
                {
                    lock (filteredDataItemsForListView)
                    {
                        for (int i = 0; i < filteredDataItemsForListView.Count; i++)
                        {
                            if ((listViewDirectories.Adapter as BrowseAdapter).SelectedPositions.Contains(i))
                            {
                                DataItem d = filteredDataItemsForListView[i];
                                FullFileInfo f = new FullFileInfo();
                                f.FileName = d.File.Filename;
                                f.FullFileName = d.Node.Data.Name + @"\" + d.File.Filename;
                                f.Size = d.File.Size;
                                slskFile.Add(f);
                            }
                        }
                    }
                }
                else
                {
                    //List<Soulseek.File> slskFile = new List<File>();
                    //List<UserFilename> = new List<UserFilename>();
                
                    lock (dataItemsForListView)
                    {
                        for(int i=0;i< dataItemsForListView.Count;i++)
                        {
                            if((listViewDirectories.Adapter as BrowseAdapter).SelectedPositions.Contains(i))
                            {
                                DataItem d = dataItemsForListView[i];
                                FullFileInfo f = new FullFileInfo();
                                f.FileName = d.File.Filename;
                                f.FullFileName = d.Node.Data.Name + @"\" + d.File.Filename;
                                f.Size = d.File.Size;
                                slskFile.Add(f);
                            }
                        }
                    }
                }
                if (MainActivity.CurrentlyLoggedInButDisconnectedState())
                {
                    //we disconnected. login then do the rest.
                    //this is due to temp lost connection
                    Task t;
                    if (!MainActivity.ShowMessageAndCreateReconnectTask(this.Context, out t))
                    {
                        return;
                    }

                    t.ContinueWith(new Action<Task>((Task t) => {
                        if (t.IsFaulted)
                        {
                            SoulSeekState.MainActivityRef.RunOnUiThread(() => {
                                //fragment.Context returns null if the fragment has not been attached, or if it got detached. (detach and attach happens on screen rotate).
                                //so best to use SoulSeekState.MainActivityRef which is static and so not null after MainActivity.OnCreate

                                Toast.MakeText(SoulSeekState.MainActivityRef, Resources.GetString(Resource.String.failed_to_connect), ToastLength.Short).Show();

                            });
                            return;
                        }
                        //CreateDownloadAllTask(slskFile.ToArray()).Start();
                        CreateDownloadAllTask(slskFile.ToArray()).Start();
                    }));
                }
                else
                {
                    //foreach(var f in slskFile.ToArray())
                    //{
                    //    MainActivity.LogDebug(f.FileName);
                    //}

                    CreateDownloadAllTask(slskFile.ToArray()).Start();
                }
            }
        }

        private void DownloadUserFilesEntryStage3(bool downloadSubfolders, List<FullFileInfo> recusiveFullFileInfo, List<FullFileInfo> topLevelFullFileInfoOnly)
        {
            if (downloadSubfolders)
            {
                if (recusiveFullFileInfo.Count == 0) //this is possible if they have a tree of folders with no files in them at all.  which would be rare but possible.
                {
                    Toast.MakeText(SoulSeekState.ActiveActivityRef, this.Resources.GetString(Resource.String.nothing_to_download), ToastLength.Long).Show();
                    return;
                }
                DownloadListOfFiles(recusiveFullFileInfo);
            }
            else
            {
                if (topLevelFullFileInfoOnly.Count == 0)
                {
                    Toast.MakeText(SoulSeekState.ActiveActivityRef, this.Resources.GetString(Resource.String.nothing_to_download), ToastLength.Long).Show();
                    return;
                }
                DownloadListOfFiles(topLevelFullFileInfoOnly);
            }
        }

        private void DownloadUserFilesEntryStage2(bool justFilteredItems)
        {
            if (justFilteredItems && filteredDataItemsForListView.Count == 0)
            {
                Toast.MakeText(SoulSeekState.ActiveActivityRef, this.Resources.GetString(Resource.String.nothing_to_download), ToastLength.Long).Show();
                return;
            }

            bool containsSubDirs = false;
            int totalItems = -1; //this one we set.
            int toplevelItems = 0; //this one we increment.
            List<FullFileInfo> recusiveFullFileInfo = new List<FullFileInfo>();
            List<FullFileInfo> topLevelFullFileInfoOnly = new List<FullFileInfo>();
            if (!justFilteredItems)
            {
                lock (dataItemsForListView)
                {
                    foreach (DataItem d in dataItemsForListView)
                    {
                        if (d.IsDirectory())
                        {
                            containsSubDirs = true;
                        }
                        else
                        {
                            FullFileInfo f = new FullFileInfo();
                            f.FileName = d.File.Filename;
                            f.FullFileName = d.Node.Data.Name + @"\" + d.File.Filename;
                            f.Size = d.File.Size;
                            topLevelFullFileInfoOnly.Add(f);
                            toplevelItems++;
                        }
                    }
                }
                if (containsSubDirs)
                {
                    recusiveFullFileInfo = GetRecursiveFullFileInfo(dataItemsForListView);
                    totalItems = recusiveFullFileInfo.Count;
                }
            }
            else
            {
                lock (filteredDataItemsForListView)
                {
                    foreach (DataItem d in filteredDataItemsForListView)
                    {
                        if (d.IsDirectory())
                        {
                            containsSubDirs = true;
                        }
                        else
                        {
                            FullFileInfo f = new FullFileInfo();
                            f.FileName = d.File.Filename;
                            f.FullFileName = d.Node.Data.Name + @"\" + d.File.Filename;
                            f.Size = d.File.Size;
                            topLevelFullFileInfoOnly.Add(f);
                            toplevelItems++;
                        }
                    }
                }
                if (containsSubDirs)
                {
                    recusiveFullFileInfo = GetRecursiveFullFileInfo(filteredDataItemsForListView);
                    totalItems = recusiveFullFileInfo.Count;
                }
            }
            bool downloadSubfolders = false;
            //show message with total num of files...
            if (containsSubDirs)
            {
                //this is Android.  There are no WinForm style blocking modal dialogs.  Show() is not synchronous.  It will not block or wait for a response.
                var builder = new Android.App.AlertDialog.Builder(SoulSeekState.ActiveActivityRef);
                builder.SetTitle("This Folder contains Subfolders");

                string topLevelStr = string.Empty;
                if (toplevelItems == 1)
                {
                    topLevelStr = string.Format(SeekerApplication.GetString(Resource.String.item_total_singular), toplevelItems);
                }
                else
                {
                    topLevelStr = string.Format(SeekerApplication.GetString(Resource.String.item_total_plural), toplevelItems);
                }
                string recursiveStr = string.Empty;
                if (totalItems == 1)
                {
                    recursiveStr = string.Format(SeekerApplication.GetString(Resource.String.item_total_singular), totalItems);
                }
                else
                {
                    recursiveStr = string.Format(SeekerApplication.GetString(Resource.String.item_total_plural), totalItems);
                }

                builder.SetMessage(string.Format(SeekerApplication.GetString(Resource.String.subfolders_warning), recursiveStr, topLevelStr));
                EventHandler<DialogClickEventArgs> eventHandlerCurrentFolder = new EventHandler<DialogClickEventArgs>((object sender, DialogClickEventArgs okayArgs) =>
                {
                    DownloadUserFilesEntryStage3(false, recusiveFullFileInfo, topLevelFullFileInfoOnly);
                });
                EventHandler<DialogClickEventArgs> eventHandlerRecursiveFolders = new EventHandler<DialogClickEventArgs>((object sender, DialogClickEventArgs okayArgs) =>
                {
                    DownloadUserFilesEntryStage3(true, recusiveFullFileInfo, topLevelFullFileInfoOnly);
                });
                builder.SetPositiveButton(Resource.String.all, eventHandlerRecursiveFolders);
                builder.SetNegativeButton(Resource.String.current_folder_only, eventHandlerCurrentFolder);
                builder.Show();
            }
            else
            {
                DownloadUserFilesEntryStage3(false, recusiveFullFileInfo, topLevelFullFileInfoOnly);
            }
        }

        private void DownloadUserFilesEntry()
        {
            if (dataItemsForListView.Count == 0)
            {
                Toast.MakeText(SoulSeekState.ActiveActivityRef, this.Resources.GetString(Resource.String.nothing_to_download), ToastLength.Long).Show();
                return;
            }
            if(FilteredResults && (dataItemsForListView.Count != filteredDataItemsForListView.Count))
            {
                //this is Android.  There are no WinForm style blocking modal dialogs.  Show() is not synchronous.  It will not block or wait for a response.
                var b = new Android.App.AlertDialog.Builder(SoulSeekState.ActiveActivityRef);
                b.SetTitle(Resource.String.filter_is_on);
                b.SetMessage(Resource.String.filter_is_on_body);
                EventHandler<DialogClickEventArgs> eventHandlerAll = new EventHandler<DialogClickEventArgs>((object sender, DialogClickEventArgs okayArgs) =>
                {
                    DownloadUserFilesEntryStage2(false);
                });
                EventHandler<DialogClickEventArgs> eventHandlerFiltered = new EventHandler<DialogClickEventArgs>((object sender, DialogClickEventArgs okayArgs) =>
                {
                    DownloadUserFilesEntryStage2(true);
                });
                b.SetPositiveButton(Resource.String.just_filtered, eventHandlerFiltered);
                b.SetNegativeButton(Resource.String.all, eventHandlerAll);
                b.Show();
            }
            else
            {
                DownloadUserFilesEntryStage2(false);
            }


        }

        private void DownloadListOfFiles(List<FullFileInfo> slskFiles)
        {
            if (MainActivity.CurrentlyLoggedInButDisconnectedState())
            {
                //we disconnected. login then do the rest.
                //this is due to temp lost connection
                Task t;
                if (!MainActivity.ShowMessageAndCreateReconnectTask(this.Context, out t))
                {
                    return;
                }

                t.ContinueWith(new Action<Task>((Task t) => {
                    if (t.IsFaulted)
                    {
                        SoulSeekState.MainActivityRef.RunOnUiThread(() => {
                            //fragment.Context returns null if the fragment has not been attached, or if it got detached. (detach and attach happens on screen rotate).
                            //so best to use SoulSeekState.MainActivityRef which is static and so not null after MainActivity.OnCreate

                            Toast.MakeText(SoulSeekState.MainActivityRef, this.Resources.GetString(Resource.String.failed_to_connect), ToastLength.Short).Show();

                        });
                        return;
                    }
                    //CreateDownloadAllTask(slskFile.ToArray()).Start();
                    CreateDownloadAllTask(slskFiles.ToArray()).Start();
                }));
            }
            else
            {
                //CreateDownloadAllTask(slskFile.ToArray()).Start();
                CreateDownloadAllTask(slskFiles.ToArray()).Start();
            }
        }

        private void DownloadUserFiles()
        {
            if(dataItemsForListView.Count == 0)
            {
                Toast.MakeText(this.Context,this.Resources.GetString(Resource.String.nothing_to_download),ToastLength.Long).Show();
            }
            else if(dataItemsForListView[0].IsDirectory())
            {
                Toast.MakeText(this.Context, this.Resources.GetString(Resource.String.not_recursive_dirs_avaialbe_to_download), ToastLength.Long).Show();
            }
            else
            {
                //List<Soulseek.File> slskFile = new List<File>();
                //List<UserFilename> = new List<UserFilename>();
                List<FullFileInfo> slskFile = new List<FullFileInfo>();
                lock(dataItemsForListView)
                {
                foreach(DataItem d in dataItemsForListView)
                {
                    //d.Node.Data.Name is complete dirname "@@uwtsp\\music\\electronica\\manual - lost days, open skies and streaming tides [2007]"
                    //d.File.Filename is filename "1. track 1.mp3"
                    FullFileInfo f = new FullFileInfo();
                    f.FileName = d.File.Filename;
                    f.FullFileName = d.Node.Data.Name + @"\" + d.File.Filename;
                    f.Size = d.File.Size;
                    slskFile.Add(f);
                }
                }
                if (MainActivity.CurrentlyLoggedInButDisconnectedState())
                {
                    //we disconnected. login then do the rest.
                    //this is due to temp lost connection
                    Task t;
                    if(!MainActivity.ShowMessageAndCreateReconnectTask(this.Context,out t))
                    {
                        return;
                    }

                    t.ContinueWith(new Action<Task>((Task t) => {
                        if (t.IsFaulted)
                        {
                            SoulSeekState.MainActivityRef.RunOnUiThread(() => { 
                                //fragment.Context returns null if the fragment has not been attached, or if it got detached. (detach and attach happens on screen rotate).
                                //so best to use SoulSeekState.MainActivityRef which is static and so not null after MainActivity.OnCreate
                                
                                Toast.MakeText(SoulSeekState.MainActivityRef, this.Resources.GetString(Resource.String.failed_to_connect), ToastLength.Short).Show(); 
                                
                                });
                            return;
                        }
                        //CreateDownloadAllTask(slskFile.ToArray()).Start();
                        CreateDownloadAllTask(slskFile.ToArray()).Start();
                    }));
                }
                else
                {
                    //CreateDownloadAllTask(slskFile.ToArray()).Start();
                    CreateDownloadAllTask(slskFile.ToArray()).Start();
                }
            }
        }


        private Task CreateDownloadAllTask(FullFileInfo[] files)
        {
            if(username==SoulSeekState.Username)
            {
                SoulSeekState.ActiveActivityRef.RunOnUiThread(() => {Toast.MakeText(SoulSeekState.ActiveActivityRef,this.Resources.GetString(Resource.String.cannot_download_from_self),ToastLength.Long).Show(); });
                return new Task(()=>{ }); //since we call start on the task, if we call Task.Completed or Task.Delay(0) it will crash...
            }
            MainActivity.LogDebug("CreateDownloadAllTask");
            bool allExist = true; //only show the transfer exists if all transfers in question do already exist
            Task task = new Task(() => {
                foreach (FullFileInfo file in files)
                {

                    DownloadDialog.SetupAndDownloadFile(username, file.FullFileName, file.Size, 0, out bool transferExists);
                    if(!transferExists)
                    {
                        allExist = false;
                    }



                }
                Action toast1 = null;
                if (allExist)
                {
                    toast1 = new Action(() => {
                    Toast.MakeText(SoulSeekState.ActiveActivityRef, this.Resources.GetString(Resource.String.error_duplicate), ToastLength.Short).Show(); });
                
                }
                else
                {
                    toast1 = new Action(() => {
                        Toast.MakeText(SoulSeekState.ActiveActivityRef, this.Resources.GetString(Resource.String.download_is_starting), ToastLength.Short).Show();
                    });
                }

                SoulSeekState.ActiveActivityRef.RunOnUiThread(toast1);
            });
            return task;
        }

        private void UpDirectory(object sender, System.EventArgs e)
        {
            GoUpDirectory();
        }





        private void ListViewDirectories_ItemClick(object sender, AdapterView.ItemClickEventArgs e)
        {
            cachedFilteredDataItemsForListView = null;
            bool filteredResults = FilteredResults;
            DataItem itemSelected = null;
            if (filteredResults)
            {
                try
                {
                    itemSelected = filteredDataItemsForListView[e.Position];
                }
                catch(IndexOutOfRangeException) //this did happen to me.... when filtering...
                {
                    MainActivity.LogFirebase("ListViewDirectories_ItemClick position: " + e.Position + "filteredDataItemsForListView.Count: " + filteredDataItemsForListView.Count);
                    MainActivity.LogDebug("ListViewDirectories_ItemClick position: " + e.Position + "filteredDataItemsForListView.Count: " + filteredDataItemsForListView.Count);
                    return;
                }
            }
            else
            {
                itemSelected = dataItemsForListView[e.Position]; //out of bounds here...
            }

            bool isFile = false;
            lock (dataItemsForListView)
            {
                //DataItem itemSelected = dataItemsForListView[e.Position];
                if (itemSelected.IsDirectory())
                {
                    if(itemSelected.Node.Children.Count==0 && (itemSelected.Directory==null || itemSelected.Directory.FileCount==0 ))
                    {
                        //dont let them do this... if this happens then there is no way to get back up...
                        Toast.MakeText(SoulSeekState.MainActivityRef, this.Resources.GetString(Resource.String.directory_is_empty),ToastLength.Short);
                        return;
                    }
                    SaveScrollPosition();
                    dataItemsForListView.Clear();
                    if (itemSelected.Node.Children.Count != 0) //then more directories
                    {
                        foreach (TreeNode<Directory> d in itemSelected.Node.Children)
                        {
                            dataItemsForListView.Add(new DataItem(d.Data, d));
                        }
                        //here we do files as well......
                        if(itemSelected.Directory!=null && itemSelected.Directory.FileCount!=0)
                        {
                            foreach (File f in itemSelected.Directory.Files)
                            {
                                dataItemsForListView.Add(new DataItem(f, itemSelected.Node));
                            }
                        }
                    }
                    else
                    {
                        foreach (File f in itemSelected.Directory.Files)
                        {
                            dataItemsForListView.Add(new DataItem(f, itemSelected.Node));
                        }
                    }
                    if(!filteredResults)
                    {
                        SetBrowseAdapters(filteredResults, dataItemsForListView, false, false);
                        //listViewDirectories.Adapter = new BrowseAdapter(this.Context, dataItemsForListView, this);
                    }
                }
                else
                {
                    isFile = true;


                    bool alreadySelected = (this.listViewDirectories.Adapter as BrowseAdapter).SelectedPositions.Contains<int>(e.Position);
                    if (!alreadySelected)
                    {

#pragma warning disable 0618
                        if ((int)Android.OS.Build.VERSION.SdkInt >= 21)
                        {
                            e.View.Background = Resources.GetDrawable(Resource.Color.cellbackSelected, null);
                            //e.View.Background = Resources.GetDrawable(Resource.Color.cellbackSelected, null);
                        }
                        else
                        {
                            e.View.Background = Resources.GetDrawable(Resource.Color.cellbackSelected);
                            //e.View.Background = Resources.GetDrawable(Resource.Color.cellbackSelected);
                        }
#pragma warning restore 0618
                        (this.listViewDirectories.Adapter as BrowseAdapter).SelectedPositions.Add(e.Position);
                    }
                    else
                    {
#pragma warning disable 0618
                        if ((int)Android.OS.Build.VERSION.SdkInt >= 21)
                        {
                            e.View.Background = null;//Resources.GetDrawable(Resource.Drawable.cell_shape_dldiag, null);
                            //e.View.Background = Resources.GetDrawable(Resource.Drawable.cell_shape_dldiag, null);
                        }
                        else
                        {
                            e.View.Background = null;//Resources.GetDrawable(Resource.Color.cellback);
                            //e.View.Background = Resources.GetDrawable(Resource.Color.cellback);
                        }
#pragma warning restore 0618
                        (this.listViewDirectories.Adapter as BrowseAdapter).SelectedPositions.Remove(e.Position);
                    }

                }
                
            }
            lock(dataItemsForListView)
            {
                lock(filteredDataItemsForListView)
                {
                    if(!isFile && filteredResults)
                    {
                        SetBrowseAdapters(filteredResults, dataItemsForListView, false, false);
                    }
                }
            }
        }

        private void ClearAllSelectedPositions()
        {
            //nullref crash was here.. not worth crashing over...
            if(listViewDirectories==null)
            {
                return;
            }
            for(int i=0;i<listViewDirectories.Count;i++)
            {
                View v = listViewDirectories.GetChildAt(i);
                if(v!=null)
                {
                    listViewDirectories.GetChildAt(i).Background = null;
                }
            }
        }

        public bool BackButton()
        {
            return GoUpDirectory();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>whether we can successfully go up.</returns>
        private bool GoUpDirectory(int additionalLevels=0)
        {
            cachedFilteredDataItemsForListView = null;
            bool filteredResults = FilteredResults;
            lock (dataItemsForListView)
            {
                TreeNode<Directory> item = null;
                try
                {
                    //var testItem = dataItemsForListView[0]?.Node;
                    if (dataItemsForListView[0].File != null)
                    {
                        item = dataItemsForListView[0]?.Node?.Parent;  //?.Parent; //This used to do grandparent.  Which is a bug I think, so I changed it.
                    }
                    else if (dataItemsForListView[0].Directory != null)
                    {
                        item = dataItemsForListView[0]?.Node?.Parent?.Parent;
                    }
                    else
                    {
                        return false;
                    }
                }
                catch
                {
                    return false; //bad... 
                }
                if (item == null)
                {
                    return false; //we must be at or near the highest
                }
                for(int i=0;i<additionalLevels;i++)
                {
                    item = item.Parent;
                }
                dataItemsForListView.Clear();
                
                foreach (TreeNode<Directory> d in item.Children)
                {
                    dataItemsForListView.Add(new DataItem(d.Data, d));
                }

                //here we do files as well......
                if (item.Data != null && item.Data.FileCount != 0)
                {
                    foreach (File f in item.Data.Files)
                    {
                        dataItemsForListView.Add(new DataItem(f, item));
                    }
                }
                if (!filteredResults)
                {
                    SetBrowseAdapters(filteredResults, dataItemsForListView, false, true);
                    //listViewDirectories.Adapter = new BrowseAdapter(this.Context, dataItemsForListView, this);
                }
                
            }
            lock(dataItemsForListView)
            {
                lock(filteredDataItemsForListView)
                {
                    SetBrowseAdapters(filteredResults, dataItemsForListView, false, true);
                    //if (filteredResults)
                    //{
                    //    filteredDataItemsForListView = FilterBrowseList(dataItemsForListView);
                    //    listViewDirectories.Adapter = new BrowseAdapter(this.Context, filteredDataItemsForListView, this);
                    //}
                }
            }
            RestoreScrollPosition();

            return true;
        }

        /// <summary>
        /// Sets both the main and the Path Items adapters.  necessary when first loading or when going up or down directories (i.e. if path changes).  not necessary if just changing the filter.
        /// </summary>
        /// <param name="toFilter"></param>
        /// <param name="nonFilteredItems"></param>
        public void SetBrowseAdapters(bool toFilter, List<DataItem> nonFilteredItems, bool fullRefreshOfPathItems, bool goingUp=false)
        {
            if (toFilter)
            {
                filteredDataItemsForListView = FilterBrowseList(dataItemsForListView);
                listViewDirectories.Adapter = new BrowseAdapter(this.Context, filteredDataItemsForListView, this);
            }
            else
            {
                listViewDirectories.Adapter = new BrowseAdapter(this.Context, dataItemsForListView, this);
            }

            var items = GetPathItems(dataItemsForListView);
            pathItems.Clear();
            pathItems.AddRange(items);
            if (fullRefreshOfPathItems)
            {
                treePathRecyclerAdapter.NotifyDataSetChanged();
            }
            else if(goingUp)
            {
                treePathRecyclerAdapter.NotifyDataSetChanged(); //removed individual updates due to weird graphical glitches...
                //treePathRecyclerAdapter.NotifyItemChanged(pathItems.Count-1); //since now the last node (remove separator)
                //treePathRecyclerAdapter.NotifyItemRemoved(pathItems.Count);
            }
            else
            {
                treePathRecyclerAdapter.NotifyDataSetChanged();
                //treePathRecyclerAdapter.NotifyItemChanged(pathItems.Count - 2); //since now no longer the last node (add separator)
                //treePathRecyclerAdapter.NotifyItemInserted(pathItems.Count - 1);
                treePathRecyclerView.ScrollToPosition(pathItems.Count - 1);
            }
            //List<PathItem> pathItems = GetPathItems(dataItemsForListView);

        }

        public class PathItem
        {
            public string DisplayName;
            public bool IsLastNode;
            public PathItem(string displayName, bool isLastNode)
            {
                DisplayName = displayName;
                IsLastNode = isLastNode;
            }
        }

        public static List<PathItem> GetPathItems(List<DataItem> nonFilteredDataItemsForListView)
        {
            List<PathItem> pathItemsList = new List<PathItem>();
            if(nonFilteredDataItemsForListView[0].IsDirectory())
            {
                GetPathItemsInternal(pathItemsList, nonFilteredDataItemsForListView[0].Node.Parent, true);
            }
            else
            {
                GetPathItemsInternal(pathItemsList, nonFilteredDataItemsForListView[0].Node, true);
            }
            pathItemsList.Reverse();
            FixNullRootDisplayName(pathItemsList);
            return pathItemsList;
        }

        private static void FixNullRootDisplayName(List<PathItem> pathItemsList)
        {
            if(pathItemsList.Count>0 && pathItemsList[0].DisplayName == string.Empty)
            {
                pathItemsList[0].DisplayName = "root";
            }
        }

        private static void GetPathItemsInternal(List<PathItem> pathItems, TreeNode<Directory> treeNode, bool lastChild)
        {
            string displayName = Helpers.GetFileNameFromFile(treeNode.Data.Name);
            pathItems.Add(new PathItem(displayName, lastChild));
            if (treeNode.Parent==null)
            {
                return;
            }
            else
            {
                GetPathItemsInternal(pathItems, treeNode.Parent, false);
            }
        }


        private TreeNode<Directory> GetNodeByName(TreeNode<Directory> rootTree, string nameToFindDirName)
        {
            if(rootTree.Data.Name == nameToFindDirName)
            {
                return rootTree;
            }
            else
            {
                foreach(TreeNode<Directory> d in rootTree.Children)
                {
                    var node = GetNodeByName(d, nameToFindDirName);
                    if(node!=null)
                    {
                        return node;
                    }
                }
            }
            return null;
        }

        private static void ClearFilterStringAndCached(bool force = false)
        {
            FilterString = string.Empty;
            FilteredResults = false;
            WordsToAvoid.Clear();
            WordsToInclude.Clear();
            //FilterSpecialFlags.Clear();
            if(BrowseFragment.Instance != null && BrowseFragment.Instance.rootView != null) //if you havent been there it will be null.
            {
                SoulSeekState.MainActivityRef.RunOnUiThread(() => {
                    EditText filterText = BrowseFragment.Instance.rootView.FindViewById<EditText>(Resource.Id.filterText);
                    filterText.Text = string.Empty;
                });
            }
        }

        public static void SoulSeekState_BrowseResponseReceived(object sender, BrowseResponseEvent e)
        {
            ClearFilterStringAndCached();
            ScrollPositionRestore?.Clear();
            ScrollPositionRestoreRotate = null;
            filteredDataItemsForListView = new List<DataItem>();
            cachedFilteredDataItemsForListView = null;
            CurrentUsername = e.Username;
            diagnostics_count = e.OriginalBrowseResponse.DirectoryCount;
            //OriginalBrowseResponse = e.OriginalBrowseResponse;
            //OurCurrentLocation = e.BrowseResponseTree; //aka root
            lock (dataItemsForListView) //on non UI thread.
            {
                dataItemsForListView.Clear();//clear old
                //originalBrowseTree = e.BrowseResponseTree; //the already parsed tree
                username = e.Username;
                if(e.StartingLocation!=null&&e.StartingLocation!=string.Empty)
                {
                    var staringPoint = BrowseFragment.Instance.GetNodeByName(e.BrowseResponseTree, e.StartingLocation);

                    if(staringPoint==null)
                    {
                        MainActivity.LogFirebase("SoulSeekState_BrowseResponseReceived: startingPoint is null");
                        SoulSeekState.MainActivityRef.RunOnUiThread(() => { Toast.MakeText(SoulSeekState.MainActivityRef, SoulSeekState.MainActivityRef.Resources.GetString(Resource.String.error_browse_at_location),ToastLength.Long).Show(); });
                        return; //we might be in a bad state just returning like this... idk...
                    }

                    //**added bc if someone wants to browse at folder and there are other folders then they will not see them....
                    foreach(TreeNode<Directory> d in staringPoint.Children)
                    {
                        dataItemsForListView.Add(new DataItem(d.Data, d));
                    }
                    //**added bc if someone wants to browse at folder and there are other folders then they will not see them....


                    foreach (File f in staringPoint.Data.Files)
                    {
                        dataItemsForListView.Add(new DataItem(f, staringPoint));
                    }
                }
                else
                {
                    foreach(TreeNode<Directory> d in e.BrowseResponseTree.Children)
                    {
                        dataItemsForListView.Add(new DataItem(d.Data,d));
                    }

                    //here we do files as well......  **I added this bc on your first browse you will not get any root dir files....
                    if (e.BrowseResponseTree.Data != null && e.BrowseResponseTree.Data.FileCount != 0)
                    {
                        foreach (File f in e.BrowseResponseTree.Data.Files)
                        {
                            dataItemsForListView.Add(new DataItem(f, e.BrowseResponseTree));
                        }
                    }
                    //**I added this bc on your first browse you will not get any root dir files....
                }
            }

            BrowseResponseReceivedUI?.Invoke(null, new EventArgs());
        }

        public void RefreshOnRecieved()
        {
            if (noBrowseView != null)
            {
                noBrowseView.Visibility = ViewStates.Gone;
                separator.Visibility = ViewStates.Visible;
            }
            listViewDirectories = rootView.FindViewById<ListView>(Resource.Id.listViewDirectories);
            //if(!tempHackItemClick)
            //{
            listViewDirectories.ItemClick -= ListViewDirectories_ItemClick;
            listViewDirectories.ItemClick += ListViewDirectories_ItemClick; //tempHackItemClick =true; 
                                                                            //}

            //!!!collection was modified exception!!!
            //guessing from modifying dataItemsForListView which can happen in this method and in others...
            currentUsernameUI = CurrentUsername;
            SetBrowseAdapters(false, dataItemsForListView, true);
            //listViewDirectories.Adapter = new BrowseAdapter(this.Context, dataItemsForListView, this); //on UI thread. in a lock.
        }

        public class FullFileInfo
        {
            public long Size = 0;
            public string FileName = string.Empty;
            public string FullFileName = string.Empty;
        }

        public class DataItem
        {
            private string Name = "";
            public Directory Directory;
            public Soulseek.File File;
            public TreeNode<Directory> Node;
            public DataItem(Directory d, TreeNode<Directory> n)
            {
                Name = d.Name; //this is the full name (other than file).. i.e. primary:Music\\Soulseek Complete
                Directory = d;
                Node = n;
            }
            public DataItem(Soulseek.File f, TreeNode<Directory> n)
            {
                Name = f.Filename;
                File = f;
                Node = n;
            }
            public bool IsDirectory()
            {
                return Directory!=null;
            }
            public string GetDisplayName()
            {
                if(IsDirectory())
                {
                    return Helpers.GetFileNameFromFile(Name);
                }
                else
                {
                    return Name;
                }
            }
        }


        public class TreePathRecyclerAdapter : RecyclerView.Adapter
        {
            private List<PathItem> localDataSet; //tab id's
            public override int ItemCount => localDataSet.Count;
            private int position = -1;
            public BrowseFragment Owner;
            public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType) //so view Type is a real thing that the recycler adapter knows about.
            {

                TreePathItemView view = TreePathItemView.inflate(parent);
                view.setupChildren();
                view.ViewFolderName.Click += View_Click;
                // .inflate(R.layout.text_row_item, viewGroup, false);
                //(view as SearchTabView).searchTabLayout.Click += SearchTabLayout_Click;
                //(view as SearchTabView).removeSearch.Click += RemoveSearch_Click;
                return new TreePathItemViewHolder(view as View);


            }

            private void View_Click(object sender, EventArgs e)
            {
                int pos = ((sender as TextView).Parent.Parent as TreePathItemView).ViewHolder.AdapterPosition;
                Owner.GoUpDirectory(localDataSet.Count - pos - 2);
            }

            public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
            {
                (holder as TreePathItemViewHolder).pathItemView.setItem(localDataSet[position]);
            }


            //private void SearchTabLayout_Click(object sender, EventArgs e)
            //{
            //    position = ((sender as View).Parent.Parent as SearchTabView).ViewHolder.AdapterPosition;
            //    int tabToGoTo = localDataSet[position];
            //    SearchFragment.Instance.GoToTab(tabToGoTo, false);
            //    SearchTabDialog.Instance.Dismiss();
            //}

            public TreePathRecyclerAdapter(List<PathItem> ti, BrowseFragment owner)
            {
                Owner = owner;
                localDataSet = ti;
            }

        }

        public class TreePathItemViewHolder : RecyclerView.ViewHolder
        {
            public TreePathItemView pathItemView;


            public TreePathItemViewHolder(View view) : base(view)
            {
                //super(view);
                // Define click listener for the ViewHolder's View

                pathItemView = (TreePathItemView)view;
                pathItemView.ViewHolder = this;
                //(ChatroomOverviewView as View).SetOnCreateContextMenuListener(this);
            }

            public TreePathItemView getUnderlyingView()
            {
                return pathItemView;
            }
        }



        public class TreePathItemView : LinearLayout
        {
            //public TransfersFragment.TransferViewHolder ViewHolder { get; set; }
            private ImageView viewSeparator;
            public TextView ViewFolderName;
            public PathItem InnerPathItem { get; set; }
            public TreePathItemViewHolder ViewHolder;

            public TreePathItemView(Context context, IAttributeSet attrs, int defStyle) : base(context, attrs, defStyle)
            {
                LayoutInflater.From(context).Inflate(Resource.Layout.tree_path_item_view, this, true);
                setupChildren();
            }
            public TreePathItemView(Context context, IAttributeSet attrs) : base(context, attrs)
            {
                LayoutInflater.From(context).Inflate(Resource.Layout.tree_path_item_view, this, true);
                setupChildren();
            }

            public static TreePathItemView inflate(ViewGroup parent)
            {
                TreePathItemView itemView = (TreePathItemView)LayoutInflater.From(parent.Context).Inflate(Resource.Layout.tree_path_item_view_dummy, parent, false);
                return itemView;
            }

            public void setupChildren()
            {
                viewSeparator = FindViewById<ImageView>(Resource.Id.folderSeparator);
                ViewFolderName = FindViewById<TextView>(Resource.Id.folderName);
            }

            public void setItem(PathItem item)
            {
                InnerPathItem = item;
                ViewFolderName.Text = item.DisplayName;
                if(item.IsLastNode)
                {
                    ViewFolderName.Clickable = false;
                    viewSeparator.Visibility = ViewStates.Gone;
                }
                else
                {
                    ViewFolderName.Clickable = true;
                    viewSeparator.Visibility = ViewStates.Visible;
                }
            }
        }



        public class BrowseAdapter : ArrayAdapter<DataItem>
        {
            public List<int> SelectedPositions = new List<int>();
            public BrowseFragment Owner = null;
            public BrowseAdapter(Context c, List<DataItem> items, BrowseFragment owner) : base(c, 0, items)
            {
                Owner = owner;
            }

            public BrowseAdapter(Context c, List<DataItem> items, BrowseFragment owner, int[]? selectedPos) : base(c, 0, items)
            {
                Owner = owner;
                if(selectedPos!=null && selectedPos.Count()!=0)
                {
                    SelectedPositions = selectedPos.ToList();
                }
            }

            public override View GetView(int position, View convertView, ViewGroup parent)
            {
                BrowseResponseItemView itemView = (BrowseResponseItemView)convertView;
                if (null == itemView) //we do this once
                {
                    itemView = BrowseResponseItemView.inflate(parent);
                    itemView.setupChildren();
                    if (SoulSeekState.InDarkModeCache)
                    {
                        itemView.DisplayName.SetTextColor(Android.Graphics.Color.White);
                    }
                    else
                    {
                        itemView.DisplayName.SetTextColor(Android.Graphics.Color.Black);
                    }
                }
                if (SelectedPositions.Contains(position)) //we do this every time.
                {
#pragma warning disable 0618
                    if ((int)Android.OS.Build.VERSION.SdkInt >= 21)
                    {
                        itemView.DisplayName.Background = Owner.Resources.GetDrawable(Resource.Color.cellbackSelected, null);
                    }
                    else
                    {
                        itemView.DisplayName.Background = Owner.Resources.GetDrawable(Resource.Color.cellbackSelected);
                    }
#pragma warning restore 0618
                }
                else
                {
                    //REMEMBER: itemViews get reused.  and so if you set the background on tap
                    //it will be set for every nth item.. and so reseting the background (as we do here)
                    //is necessary!
                    if ((int)Android.OS.Build.VERSION.SdkInt >= 21)
                    {
                        itemView.DisplayName.Background = null;//Resources.GetDrawable(Resource.Drawable.cell_shape_dldiag, null);
                                                   //e.View.Background = Resources.GetDrawable(Resource.Drawable.cell_shape_dldiag, null);
                    }
                    else
                    {
                        itemView.DisplayName.Background = null;//Resources.GetDrawable(Resource.Color.cellback);
                                                   //e.View.Background = Resources.GetDrawable(Resource.Color.cellback);
                    }
                }
                var dataItem = GetItem(position);
                if (dataItem.IsDirectory())
                {
                    itemView.FolderIndicator.Visibility = ViewStates.Visible;
                }
                else
                {
                    itemView.FolderIndicator.Visibility = ViewStates.Gone;
                }
                itemView.DisplayName.Text = dataItem.GetDisplayName();
                return itemView;
                //return base.GetView(position, convertView, parent);
            }
        }

        private static AndroidX.AppCompat.App.AlertDialog browseUserDialog = null;

        public void ShowEditTextBrowseUserDialog()
        {
            //AndroidX.AppCompat.App.AlertDialog.Builder builder = new AndroidX.AppCompat.App.AlertDialog.Builder(); //failed to bind....
            FragmentActivity c = this.Activity != null ? this.Activity : SoulSeekState.MainActivityRef;
            MainActivity.LogInfoFirebase("ShowEditTextBrowseUserDialog" + c.IsDestroyed + c.IsFinishing);
            AndroidX.AppCompat.App.AlertDialog.Builder builder = new AndroidX.AppCompat.App.AlertDialog.Builder(c); //failed to bind....
            builder.SetTitle(c.Resources.GetString(Resource.String.browse_user_files));
            // I'm using fragment here so I'm using getView() to provide ViewGroup
            // but you can provide here any other instance of ViewGroup from your Fragment / Activity
            View viewInflated = LayoutInflater.From(c).Inflate(Resource.Layout.browse_chosen_user, (ViewGroup)this.View, false);
            // Set up the input
            AutoCompleteTextView input = (AutoCompleteTextView)viewInflated.FindViewById<AutoCompleteTextView>(Resource.Id.chosenUserEditText);
            SeekerApplication.SetupRecentUserAutoCompleteTextView(input);
            
            // Specify the type of input expected; this, for example, sets the input as a password, and will mask the text
            builder.SetView(viewInflated);

            Action<View> goSnackBarAction = new Action<View>((View v) => { 
                ((Android.Support.V4.View.ViewPager)(SoulSeekState.MainActivityRef.FindViewById(Resource.Id.pager))).SetCurrentItem(3, true); });

            EventHandler<DialogClickEventArgs> eventHandler = new EventHandler<DialogClickEventArgs>((object sender, DialogClickEventArgs okayArgs) =>
            {
                //Do the Browse Logic...
                string usernameToBrowse = input.Text;
                if(usernameToBrowse==null|| usernameToBrowse==string.Empty)
                {
                    Toast.MakeText(this.Activity != null ? this.Activity : SoulSeekState.MainActivityRef, SoulSeekState.MainActivityRef.Resources.GetString(Resource.String.must_type_a_username_to_browse), ToastLength.Short).Show();
                    if (sender is AndroidX.AppCompat.App.AlertDialog aDiag1) //actv
                    {
                        aDiag1.Dismiss();
                    }
                    else
                    {
                        BrowseFragment.browseUserDialog.Dismiss();
                    }
                    return;
                }
                SoulSeekState.RecentUsersManager.AddUserToTop(usernameToBrowse, true);
                DownloadDialog.RequestFilesApi(usernameToBrowse, this.View, goSnackBarAction, null);
                if(sender is AndroidX.AppCompat.App.AlertDialog aDiag)
                {
                    aDiag.Dismiss();
                }
                else
                {
                    BrowseFragment.browseUserDialog.Dismiss();
                }
            });
            EventHandler<DialogClickEventArgs> eventHandlerCancel = new EventHandler<DialogClickEventArgs>((object sender, DialogClickEventArgs cancelArgs) =>
            {
                if (sender is AndroidX.AppCompat.App.AlertDialog aDiag)
                {
                    aDiag.Dismiss();
                }
                else
                {
                    BrowseFragment.browseUserDialog.Dismiss();
                }
            });

            System.EventHandler<TextView.EditorActionEventArgs> editorAction =  (object sender, TextView.EditorActionEventArgs e) =>
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
                        imm.HideSoftInputFromWindow(rootView.WindowToken, 0);
                    }
                    catch (System.Exception ex)
                    {
                        MainActivity.LogFirebase(ex.Message + " error closing keyboard");
                    }
                    //Do the Browse Logic...
                    eventHandler(sender,null);
                }
            };

            System.EventHandler < TextView.KeyEventArgs > keypressAction = (object sender, TextView.KeyEventArgs e) =>
            {
                if (e.Event != null && e.Event.Action == KeyEventActions.Up && e.Event.KeyCode == Keycode.Enter)
                {
                    MainActivity.LogDebug("keypress: " + e.Event.KeyCode.ToString());
                    //rootView.FindViewById<EditText>(Resource.Id.filterText).ClearFocus();
                    //rootView.FindViewById<View>(Resource.Id.focusableLayout).RequestFocus();
                    //overriding this, the keyboard fails to go down by default for some reason.....
                    try
                    {
                        Android.Views.InputMethods.InputMethodManager imm = (Android.Views.InputMethods.InputMethodManager)SoulSeekState.MainActivityRef.GetSystemService(Context.InputMethodService);
                        imm.HideSoftInputFromWindow(rootView.WindowToken, 0);
                    }
                    catch (System.Exception ex)
                    {
                        MainActivity.LogFirebase(ex.Message + " error closing keyboard");
                    }
                    //Do the Browse Logic...
                    eventHandler(sender, null);
                }
                else
                {
                    e.Handled = false;
                }
            };

            input.KeyPress += keypressAction;
            input.EditorAction += editorAction;
            input.FocusChange += Input_FocusChange;

            builder.SetPositiveButton(this.Resources.GetString(Resource.String.okay), eventHandler);
            builder.SetNegativeButton(this.Resources.GetString(Resource.String.cancel), eventHandlerCancel);
            // Set up the buttons

            BrowseFragment.browseUserDialog = builder.Create();
            try
            {
                BrowseFragment.browseUserDialog.Show();
            }
            catch(WindowManagerBadTokenException e)
            {
                if(SoulSeekState.MainActivityRef==null || this.Activity==null)
                {
                    MainActivity.LogFirebase("WindowManagerBadTokenException null activities");
                }
                else
                {
                    bool isCachedMainActivityFinishing = SoulSeekState.MainActivityRef.IsFinishing;
                    bool isOurActivityFinishing = this.Activity.IsFinishing;
                    MainActivity.LogFirebase("WindowManagerBadTokenException are we finishing:" + isCachedMainActivityFinishing + isOurActivityFinishing);
                }
            }
            catch(Exception err)
            {
                if (SoulSeekState.MainActivityRef == null || this.Activity == null)
                {
                    MainActivity.LogFirebase("Exception null activities");
                }
                else
                {
                    bool isCachedMainActivityFinishing = SoulSeekState.MainActivityRef.IsFinishing;
                    bool isOurActivityFinishing = this.Activity.IsFinishing; 
                    MainActivity.LogFirebase("Exception are we finishing:" + isCachedMainActivityFinishing + isOurActivityFinishing);
                }
            }
            
        }



        private void Input_FocusChange(object sender, View.FocusChangeEventArgs e)
        {
            try
            {
                SoulSeekState.MainActivityRef.Window.SetSoftInputMode(SoftInput.AdjustNothing);
            }
            catch (System.Exception err)
            {
                MainActivity.LogFirebase("MainActivity_FocusChange" + err.Message);
            }
        }

        //private void Input_EditorAction(object sender, TextView.EditorActionEventArgs e)
        //{
        //     if (e.ActionId == Android.Views.InputMethods.ImeAction.Done || //in this case it is Done (blue checkmark)
        //         e.ActionId == Android.Views.InputMethods.ImeAction.Go ||
        //         e.ActionId == Android.Views.InputMethods.ImeAction.Next ||
        //         e.ActionId == Android.Views.InputMethods.ImeAction.Search)
        //     {
        //         MainActivity.LogDebug("IME ACTION: " + e.ActionId.ToString());
        //         //rootView.FindViewById<EditText>(Resource.Id.filterText).ClearFocus();
        //         //rootView.FindViewById<View>(Resource.Id.focusableLayout).RequestFocus();
        //         //overriding this, the keyboard fails to go down by default for some reason.....
        //         try
        //         {
        //             Android.Views.InputMethods.InputMethodManager imm = (Android.Views.InputMethods.InputMethodManager)SoulSeekState.MainActivityRef.GetSystemService(Context.InputMethodService);
        //             imm.HideSoftInputFromWindow(rootView.WindowToken, 0);
        //         }
        //         catch (System.Exception ex)
        //         {
        //             MainActivity.LogFirebase(new Java.Lang.Throwable(ex.Message + " error closing keyboard"));
        //         }
        //        //Do the Browse Logic...
        //        string usernameToBrowse = input.Text;
        //        if (usernameToBrowse == null || usernameToBrowse == string.Empty)
        //        {
        //            Toast.MakeText(SoulSeekState.MainActivityRef, "Must type User to Browse.", ToastLength.Short);
        //            (sender as AndroidX.AppCompat.App.AlertDialog).Dismiss();
        //            return;
        //        }
        //        DownloadDialog.RequestFilesApi(usernameToBrowse, this.View, goSnackBarAction, null);
        //        (sender as AndroidX.AppCompat.App.AlertDialog).Dismiss();
        //    }
        //}
    }


    public class BrowseResponseItemView : LinearLayout
    {
        public TextView DisplayName;
        public ImageView FolderIndicator;
        public BrowseResponseItemView(Context context, IAttributeSet attrs, int defStyle) : base(context, attrs, defStyle)
        {
            LayoutInflater.From(context).Inflate(Resource.Layout.browse_response_item, this, true);
            setupChildren();
        }
        public BrowseResponseItemView(Context context, IAttributeSet attrs) : base(context, attrs)
        {
            LayoutInflater.From(context).Inflate(Resource.Layout.browse_response_item, this, true);
            setupChildren();
        }

        public static BrowseResponseItemView inflate(ViewGroup parent)
        {
            BrowseResponseItemView itemView = (BrowseResponseItemView)LayoutInflater.From(parent.Context).Inflate(Resource.Layout.browse_response_item_dummy, parent, false);
            return itemView;
        }

        public void setupChildren()
        {
            DisplayName = FindViewById<TextView>(Resource.Id.displayName);
            FolderIndicator = FindViewById<ImageView>(Resource.Id.folderIndicator);
        }
    }
}