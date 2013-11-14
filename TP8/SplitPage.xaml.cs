﻿using TP8.Data;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Text.RegularExpressions;
using Windows.UI.Popups;

// The Split Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234234

namespace TP8
{
    /// <summary>
    /// A page that displays a group title, a list of items within the group, and details for the
    /// currently selected item.
    /// </summary>
    public sealed partial class SplitPage : TP8.Common.LayoutAwarePage
    {
        // Used with bottom app bar:
        private Popup discardMenuPopUp = null;
        private Popup whyDiscardedPopUp = null;
        private string uuid = "";

        public SplitPage()
        {
            this.InitializeComponent();
        }

        #region Page state management

        /// <summary>
        /// Populates the page with content passed during navigation.  Any saved state is also
        /// provided when recreating a page from a prior session.
        /// </summary>
        /// <param name="navigationParameter">The parameter value passed to
        /// <see cref="Frame.Navigate(Type, Object)"/> when this page was initially requested.
        /// </param>
        /// <param name="pageState">A dictionary of state preserved by this page during an earlier
        /// session.  This will be null the first time a page is visited.</param>
        protected override void LoadState(Object navigationParameter, Dictionary<String, Object> pageState)
        {
            // TODO: Create an appropriate data model for your problem domain to replace the sample data
            var group = SampleDataSource.GetGroup((String)navigationParameter);
            this.DefaultViewModel["Group"] = group;
            this.DefaultViewModel["Items"] = group.Items;

            // Dunno if this is a good idea, but need to get group for sort flyout:
            App.CurrentSearchResultsGroupName =(String)navigationParameter;

            // maybe "current event only" checkbox or sort order has changed.  This will affect contents of groups fetched below.
            App.PatientDataGroups.ReSortAndMinimallyFilter(); // filter is only "current event only" checkbox
            SampleDataSource.RefreshOutboxAndAllStationsItems(); // Propagate here

            if (pageState == null)
            {
                this.itemListView.SelectedItem = null;
                // When this is a new page, select the first item automatically unless logical page
                // navigation is being used (see the logical page navigation #region below.)
                if (!this.UsingLogicalPageNavigation() && this.itemsViewSource.View != null)
                {
                    this.itemsViewSource.View.MoveCurrentToFirst();
                }
            }
            else
            {
                // Restore the previously saved state associated with this page
                if (pageState.ContainsKey("SelectedItem") && this.itemsViewSource.View != null)
                {
                    var selectedItem = SampleDataSource.GetItem((String)pageState["SelectedItem"]);
                    this.itemsViewSource.View.MoveCurrentTo(selectedItem);
                }
            }
            SetEventAndOrgText(); // Based on App.CheckBoxCurrentEventOnly
            CheckBoxCurrentEventOnlyPortrait.IsChecked = CheckBoxCurrentEventOnly.IsChecked = App.OutboxCheckBoxCurrentEventOnly;
            CheckBoxMyOrgOnlyPortrait.IsChecked = CheckBoxMyOrgOnly.IsChecked = App.OutboxCheckBoxMyOrgOnly;
            sortedByTextPortrait.Text = sortedByText.Text = "Sorted " + App.PatientDataGroups.GetShortSortDescription(); // Similar to DefaultViewModel["QueryText"] in SearchResultsPage
        }

        /// <summary>
        /// Preserves state associated with this page in case the application is suspended or the
        /// page is discarded from the navigation cache.  Values must conform to the serialization
        /// requirements of <see cref="SuspensionManager.SessionState"/>.
        /// </summary>
        /// <param name="pageState">An empty dictionary to be populated with serializable state.</param>
        protected override void SaveState(Dictionary<String, Object> pageState)
        {
            if (this.itemsViewSource.View != null)
            {
                var selectedItem = (SampleDataItem)this.itemsViewSource.View.CurrentItem;
                if (selectedItem != null) pageState["SelectedItem"] = selectedItem.UniqueId;
            }
        }


        #endregion

        #region TopAppBar
        // Attempts to make nav bar global not yet successful
        private void Checklist_Click(object sender, RoutedEventArgs e) // at moment, Home icon on nav bar
        {
            this.Frame.Navigate(typeof(BasicPageChecklist), "pageChecklist");
        }

        private void New_Click(object sender, RoutedEventArgs e)  // at moment, Webcam icon on nav bar
        {
            this.Frame.Navigate(typeof(BasicPageNew), "pageNewReport");
        }

        private void AllStations_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(SplitPage), "AllStations"); // Defined in SampleDataSource.cs
        }

        private void Outbox_Click(object sender, RoutedEventArgs e) // at moment, List icon on nav bar
        {
            this.Frame.Navigate(typeof(SplitPage), "Outbox"); // Defined in SampleDataSource.cs
        }

        private void Statistics_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(SplitPage), "Statistics"); // Defined in SampleDataSource.cs
        }

        private void Home_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(HomeItemsPage), "AllGroups"); //"pageRoot");
        }
        #endregion

        #region BottomAppBar

        // Code in this region is also common to ViewEditReportPage
        private void Discard_Click(object sender, RoutedEventArgs e)
        {
            // This Win 8.0 method is based on sample from http://www.csharpteacher.com/2013/04/how-to-add-menu-to-app-bar-windows-8.html
            // We'd do it a different way, with a XAML CommandBar, if restricting to 8.1
            // More complex version:  http://weblogs.asp.net/broux/archive/2012/07/03/windows-8-application-bar-popup-button.aspx
            /*Popup*/

            //var dialog = new MessageDialog("Edit: TO DO");
            //var t = dialog.ShowAsync(); // Assign to t to suppress compiler warning
            SampleDataItem selectedItem = null;
            if (this.itemsViewSource.View == null || (selectedItem = (SampleDataItem)this.itemsViewSource.View.CurrentItem) == null)
            {
                var dialog = new MessageDialog("Edit: No item selected.");
                var t = dialog.ShowAsync(); // Assign to t to suppress compiler warning
                return;
            }
            App.MyAssert(App.CurrentSearchResultsGroupName.Contains("Outbox") || App.CurrentSearchResultsGroupName.Contains("AllStations"));
            if(!App.CurrentSearchResultsGroupName.Contains("Outbox"))
            {
                var dialog = new MessageDialog("Sorry, discarding a report from the 'All Stations' list is not yet implemented.  Use 'Outbox' instead.");
                var t = dialog.ShowAsync(); // Assign to t to suppress compiler warning
                return;
            }
            App.MyAssert(App.CurrentSearchResultsGroupName.Contains("Outbox"));
            int index = App.PatientDataGroups.GetOutbox().FindIndexByWhenLocalTime(selectedItem.UniqueId);  //UniqueId is WhenLocalTime
            if(index < 0) // despiration:
                App.PatientDataGroups.GetOutbox().FindIndexByPatientIDAndSentCodeVersion(selectedItem.Subtitle,1); // TO DO... handle this way better
            if(index < 0)
            {
                var dialog = new MessageDialog("Sorry, for some reason this report wasn't located.  Internal error.");
                var t = dialog.ShowAsync(); // Assign to t to suppress compiler warning
                return;
            }
            App.CurrentPatient = App.PatientDataGroups.GetOutbox().Fetch(index);

            discardMenuPopUp = new Popup();
            discardMenuPopUp.IsLightDismissEnabled = true;  // Dismiss popup automatically when user hits other part of app
            StackPanel panel = new StackPanel(); // create a panel as the root of the menu
            panel.Background = BottomAppBar.Background;
            panel.Height = 140;
            panel.Width = 180;
            Button DeleteLocalButton = new Button();
            DeleteLocalButton.Content = "From outbox only";
            DeleteLocalButton.Style = (Style)App.Current.Resources["TextButtonStyle"];
            DeleteLocalButton.Margin = new Thickness(20, 10, 20, 10);
            DeleteLocalButton.Click += DeleteLocal_Click;
            panel.Children.Add(DeleteLocalButton);
            Button DeleteRemoteTooButton = new Button();
            DeleteRemoteTooButton.Content = "From TriageTrak too";
            DeleteRemoteTooButton.Style = (Style)App.Current.Resources["TextButtonStyle"];
            DeleteRemoteTooButton.Margin = new Thickness(20, 10, 20, 10);
            DeleteRemoteTooButton.Click += DeleteRemoteToo_Click;
            panel.Children.Add(DeleteRemoteTooButton);
            // Add the root menu as the popup contents:
            discardMenuPopUp.Child = panel;
            // Calculate the location, here in the bottom righthand corner with padding of 4:
            discardMenuPopUp.HorizontalOffset = Window.Current.CoreWindow.Bounds.Right - panel.Width - 4;
            discardMenuPopUp.VerticalOffset = Window.Current.CoreWindow.Bounds.Bottom - BottomAppBar.ActualHeight - panel.Height - 4;
            discardMenuPopUp.IsOpen = true;
        }


        private async void DeleteLocal_Click(object sender, RoutedEventArgs e)
        {
            string pid = App.CurrentPatient.PatientID; // ClearEntryAll will clear these, so remember them for Discard
            int v = App.CurrentPatient.ObjSentCode.GetVersionCount();
            //ClearEntryAll();  // Will indirectly mark as altered
            //LastSentMsg.Text = "Discard from Outbox: Done.";
            App.PatientDataGroups.GetOutbox().Discard(pid, v);
            App.PatientDataGroups.ScrubOutbox(); // Discard itself doesn't seem to do it, leaves empty record behind
            await App.PatientDataGroups.GetOutbox().WriteXML();
            App.PatientDataGroups.Init2(); // resort, refilter, refresh
            discardMenuPopUp.IsOpen = false;
            TopAppBar.IsOpen = false;
            BottomAppBar.IsOpen = false;
        }

        private async void DeleteRemoteToo_Click(object sender, RoutedEventArgs e)
        {
            if (!App.goodWebServiceConnectivity)
            {
                MessageDialog dlg = new MessageDialog(
                    "Sorry, better communications with TriageTrak is needed to do this.  Discarding was cancelled.  Try again later when the 'traffic light' squares (in the New Report or Edit Report pages) are flashing green or yellow instead of red.");
                await dlg.ShowAsync();
                // We don't want to encourage doing a local delete, because that will make it harder to later do a remote delete.  So close popup, app bar.
                discardMenuPopUp.IsOpen = false;
                TopAppBar.IsOpen = false;
                BottomAppBar.IsOpen = false;
                return;
            }

            // Ask if discard locally or both places?

            uuid = await App.service.GetUuidFromPatientID(App.CurrentPatient.PatientID, App.CurrentDisaster.EventShortName);
            if (String.IsNullOrEmpty(uuid))
            {
                string errMsg = "TriageTrak can't find a record with Patient ID " + App.CurrentPatient.PatientID + " associated with event '" + App.CurrentDisaster.EventName +
                    "'.  Discarding was cancelled.  You could try again but choose 'From Outbox Only', if you think that appropriate.";
                MessageDialog dlg = new MessageDialog(errMsg);
                await dlg.ShowAsync();
                await App.ErrorLog.ReportToErrorLog("On SplitPage Prep for Discard - for event with short name " + App.CurrentDisaster.EventShortName, errMsg, true);
                // bail out:
                // Maybe not:
                // await App.PatientDataGroups.UpdateSendHistoryAfterOutbox(App.CurrentPatient.PatientID, ObjSentCode); // see explanation of this below
                discardMenuPopUp.IsOpen = false;
                TopAppBar.IsOpen = false;
                BottomAppBar.IsOpen = false;
                return;
            }

            discardMenuPopUp.IsOpen = false; // take down 1 popup, then put up another
            whyDiscardedPopUp = new Popup();
            whyDiscardedPopUp.IsLightDismissEnabled = true;  // Dismiss popup automatically when user hits other part of app
            StackPanel panel2 = new StackPanel(); // create a panel as the root of the menu
            panel2.Background = BottomAppBar.Background;
            panel2.Height = 140;
            panel2.Width = 180;
            TextBlock label = new TextBlock();
            label.Text = "Why discard? (optional explanation)";
            panel2.Children.Add(label);
            TextBox explanation = new TextBox();
            explanation.Name = "Explanation";
            panel2.Children.Add(explanation);
            Button OKButton = new Button();
            OKButton.Content = "OK";
            OKButton.Style = (Style)App.Current.Resources["TextButtonStyle"];
            OKButton.Margin = new Thickness(20, 10, 20, 10);
            OKButton.Click += FinishRemoteDiscard_Click;
            panel2.Children.Add(OKButton);

            // Add the root menu as the popup contents:
            whyDiscardedPopUp.Child = panel2;
            // Calculate the location, here in the bottom righthand corner with padding of 4:
            whyDiscardedPopUp.HorizontalOffset = Window.Current.CoreWindow.Bounds.Right - panel2.Width - 4;
            whyDiscardedPopUp.VerticalOffset = Window.Current.CoreWindow.Bounds.Bottom - BottomAppBar.ActualHeight - panel2.Height - 4;
            whyDiscardedPopUp.IsOpen = true;
        }

        private async void FinishRemoteDiscard_Click(object sender, RoutedEventArgs e) // From OK button in whyDiscardedPopUp
        {
            // Quick hack... call service directly, instead of putting request on send queue
            TextBox explanation = (TextBox)whyDiscardedPopUp.FindName("Explanation");
            App.MyAssert(explanation != null);
            await App.service.ExpirePerson(uuid, explanation.Text);
            // On to local discard....
            DeleteLocal_Click(sender, e);
            TopAppBar.IsOpen = false;
            BottomAppBar.IsOpen = false;
            whyDiscardedPopUp.IsOpen = false;
        }
        #endregion

        #region Logical page navigation

        // Visual state management typically reflects the four application view states directly
        // (full screen landscape and portrait plus snapped and filled views.)  The split page is
        // designed so that the snapped and portrait view states each have two distinct sub-states:
        // either the item list or the details are displayed, but not both at the same time.
        //
        // This is all implemented with a single physical page that can represent two logical
        // pages.  The code below achieves this goal without making the user aware of the
        // distinction.

        /// <summary>
        /// Invoked to determine whether the page should act as one logical page or two.
        /// </summary>
        /// <param name="viewState">The view state for which the question is being posed, or null
        /// for the current view state.  This parameter is optional with null as the default
        /// value.</param>
        /// <returns>True when the view state in question is portrait or snapped, false
        /// otherwise.</returns>
        private bool UsingLogicalPageNavigation(ApplicationViewState? viewState = null)
        {
            if (viewState == null) viewState = ApplicationView.Value;
            return viewState == ApplicationViewState.FullScreenPortrait ||
                viewState == ApplicationViewState.Snapped;
        }

        /// <summary>
        /// Invoked when an item within the list is selected.
        /// </summary>
        /// <param name="sender">The GridView (or ListView when the application is Snapped)
        /// displaying the selected item.</param>
        /// <param name="e">Event data that describes how the selection was changed.</param>
        void ItemListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Invalidate the view state when logical page navigation is in effect, as a change
            // in selection may cause a corresponding change in the current logical page.  When
            // an item is selected this has the effect of changing from displaying the item list
            // to showing the selected item's details.  When the selection is cleared this has the
            // opposite effect.
            if (this.UsingLogicalPageNavigation()) this.InvalidateVisualState();
        }

        /// <summary>
        /// Invoked when the page's back button is pressed.
        /// </summary>
        /// <param name="sender">The back button instance.</param>
        /// <param name="e">Event data that describes how the back button was clicked.</param>
        protected override void GoBack(object sender, RoutedEventArgs e)
        {
            if (this.UsingLogicalPageNavigation() && itemListView.SelectedItem != null)
            {
                // When logical page navigation is in effect and there's a selected item that
                // item's details are currently displayed.  Clearing the selection will return
                // to the item list.  From the user's point of view this is a logical backward
                // navigation.
                this.itemListView.SelectedItem = null;
            }
            else
            {
                // When logical page navigation is not in effect, or when there is no selected
                // item, use the default back button behavior.
                base.GoBack(sender, e);
            }
        }

        /// <summary>
        /// Invoked to determine the name of the visual state that corresponds to an application
        /// view state.
        /// </summary>
        /// <param name="viewState">The view state for which the question is being posed.</param>
        /// <returns>The name of the desired visual state.  This is the same as the name of the
        /// view state except when there is a selected item in portrait and snapped views where
        /// this additional logical page is represented by adding a suffix of _Detail.</returns>
        protected override string DetermineVisualState(ApplicationViewState viewState)
        {
            // Update the back button's enabled state when the view state changes
            var logicalPageBack = this.UsingLogicalPageNavigation(viewState) && this.itemListView.SelectedItem != null;
            var physicalPageBack = this.Frame != null && this.Frame.CanGoBack;
            this.DefaultViewModel["CanGoBack"] = logicalPageBack || physicalPageBack;

            // Determine visual states for landscape layouts based not on the view state, but
            // on the width of the window.  This page has one layout that is appropriate for
            // 1366 virtual pixels or wider, and another for narrower displays or when a snapped
            // application reduces the horizontal space available to less than 1366.
            if (viewState == ApplicationViewState.Filled ||
                viewState == ApplicationViewState.FullScreenLandscape)
            {
                var windowWidth = Window.Current.Bounds.Width;
                if (windowWidth >= 1366) return "FullScreenLandscapeOrWide";
                return "FilledOrNarrow";
            }

            // When in portrait or snapped start with the default visual state name, then add a
            // suffix when viewing details instead of the list
            var defaultStateName = base.DetermineVisualState(viewState);
            return logicalPageBack ? defaultStateName + "_Detail" : defaultStateName;
        }

        #endregion

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            TryToGoToViewEdit();
        }

        private void TryToGoToViewEdit()
        {
            //var dialog = new MessageDialog("Edit: TO DO");
            //var t = dialog.ShowAsync(); // Assign to t to suppress compiler warning
            if (this.itemsViewSource.View != null)
            {
                SampleDataItem selectedItem = (SampleDataItem)this.itemsViewSource.View.CurrentItem;
                if (selectedItem != null)
                {
                    this.Frame.Navigate(typeof(BasicPageViewEdit), selectedItem.UniqueId); // "pageViewEdit");  //UniqueId is WhenLocalTime
                    return;
                }
            }
            var dialog = new MessageDialog("Edit: No item selected.");
            var t = dialog.ShowAsync(); // Assign to t to suppress compiler warning
        }

        private void CheckBoxCurrentEventOnly_Tapped(object sender, TappedRoutedEventArgs e)
        {
            App.OutboxCheckBoxCurrentEventOnly = (bool)((CheckBox)sender).IsChecked;
            if (((CheckBox)sender).Name == "CheckBoxCurrentEventOnly") // propagate from control visible in current view mode to hidden control
                CheckBoxCurrentEventOnlyPortrait.IsChecked = App.OutboxCheckBoxCurrentEventOnly;
            else
                CheckBoxCurrentEventOnly.IsChecked = App.OutboxCheckBoxCurrentEventOnly;
            SetEventAndOrgText();
            LoadState(App.CurrentSearchResultsGroupName, null);
        }

        private void CheckBoxMyOrgOnly_Tapped(object sender, TappedRoutedEventArgs e)
        {
            App.OutboxCheckBoxMyOrgOnly = (bool)((CheckBox)sender).IsChecked;
            if (((CheckBox)sender).Name == "CheckBoxMyOrgOnly") // propagate from control visible in current view mode to hidden control
                CheckBoxMyOrgOnlyPortrait.IsChecked = App.OutboxCheckBoxMyOrgOnly;
            else
                CheckBoxMyOrgOnly.IsChecked = App.OutboxCheckBoxMyOrgOnly;
            SetEventAndOrgText();
            LoadState(App.CurrentSearchResultsGroupName, null);
        }

        private void SetEventAndOrgText()
        {
            if (App.OutboxCheckBoxCurrentEventOnly)
                eventAndOrgTextPortrait.Text = eventAndOrgText.Text = App.CurrentDisaster.EventName + ", ";
            else
                eventAndOrgTextPortrait.Text = eventAndOrgText.Text = "All Events, ";
            if (App.OutboxCheckBoxMyOrgOnly)
                eventAndOrgTextPortrait.Text = eventAndOrgText.Text += App.CurrentOrgContactInfo.OrgAbbrOrShortName;
            else
                eventAndOrgTextPortrait.Text = eventAndOrgText.Text += "All Orgs";
        }

        private async void sortFlyoutOutbox_Tapped(object sender, TappedRoutedEventArgs e)
        {
            //bool changed;
            if (ApplicationView.Value == ApplicationViewState.Snapped)
            {
                this.Frame.Navigate(typeof(SortNonFlyout), "pageSortNonFlyout");
                // may set to true: App.CurrentFilterProfile.AControlChanged
            }
            else
            {
                //Windows.UI.ViewManagement.ApplicationView.TryUnsnap();// Glenn's quick hack, since this doesn't work well while snapped
                var flyout = new SortFlyout.sFlyout();
                // we could introduce our own variable here, but we'll reuse filter's

                App.CurrentFilterProfile.AControlChanged = await flyout.ShowAsync();
            }
            if (App.CurrentFilterProfile.AControlChanged)
            {
                //GroupOrFlyoutFilterChanged(); // Search is not refreshed until flyout is dismissed.  Simple but maybe not ideal.
                LoadState(App.CurrentSearchResultsGroupName, null); // As if navigating anew to this page...to get lists
                App.CurrentFilterProfile.AControlChanged = false;
            }
        }

        private void Image_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Same App Bar Edit
            TryToGoToViewEdit();
        }
    }
}
