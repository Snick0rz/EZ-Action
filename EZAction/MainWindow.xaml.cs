﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Text.Json;
using System.Text.Json.Serialization;
using WK.Libraries.BetterFolderBrowserNS;

//using System.Timers;
using Microsoft.Win32;

using MessageBox = System.Windows.Forms.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace TestAppWPF
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	///

	public partial class MainWindow : Window
	{
		// Global flag to check if editing mode is active
		bool editing = false;           // editing flag
		bool fileOpen = false;          // file opened
		bool edited = false;            // has the file been edited?

		string targetCache;
		readonly string windowTitleBase = "Easy Action for ArmA 3";

		string openedFile = "";         // to be manipulated

		// Animation objects
		readonly DoubleAnimation fadeOutAnim = new DoubleAnimation();
		Storyboard animPlay;

		/*---------------------------------*/
		/* Constants for the utility label */
		/*---------------------------------*/
		const string labelEmpty = "";
		const string editingMode = "Editing mode active";
		const string editCancelled = "Edit cancelled";

		const string actionCreated = "Action created";
		const string actionEdited = "Action edited";
		const string actionDeleted = "Action deleted";

		const string validationError = "Action incomplete";
		const string nameValidationError = "Illegal chars found";
		const string noActionExists = "No actions exist";
		const string noActionSelected = "No action was selected";

		const string copiedToClipboard = "Copied to clipboard";
		const string actionsWritten = "Actions written to file";
		const string writeAborted = "Action write aborted";
		const string noActionsToWrite = "No actions to write";
		const string errorWrite = "Error during writing";


		/*---------------------------------*/
		/* Constants for the save button   */
		/*---------------------------------*/
		const string defaultSave = "Save Changes";
		const string createAction = "Create Action";
		//const string editAction = "Edit Action";



		public MainWindow()
		{
			InitializeComponent();

			bool profileDirInitial = Properties.Settings.Default.profileDir == "";
			bool savePathInitial = Properties.Settings.Default.defaultSavePath == "";

			bool armaProfileFound = Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\ArmA 3");

			StringBuilder builder = new StringBuilder();

			MessageBoxButtons buttons = MessageBoxButtons.OK;
			MessageBoxIcon image = MessageBoxIcon.Information;

			BetterFolderBrowser browser = new BetterFolderBrowser
			{
				Multiselect = false,
				RootFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
			};

			if (profileDirInitial && savePathInitial)
			{
				builder.AppendLine("Welcome to EZ Action!");
				builder.AppendLine("");
				if (armaProfileFound)
				{
					builder.AppendLine("The default ArmA 3 profile has been detected and chosen as your profile. You can switch to another profiles in the settings.");
					Properties.Settings.Default.profileDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\ArmA 3";
					Properties.Settings.Default.Save();
				}
				else
				{
					builder.AppendLine("It appears the default ArmA 3 profile doesn't exist. The \"go-to profile directory\" button has been disabled and no default profile has been set.");
					ArmaDir_Button.IsEnabled = false;
				}
				builder.AppendLine("");
				builder.AppendLine("Please select your working directory. All files generated by this program will be saved there.");
				builder.AppendLine("You will be able to change this selection later.");

				MessageBox.Show(builder.ToString(), "Initial Setup", buttons, image);

				DialogResult result = browser.ShowDialog();

				switch (result)
				{
					case System.Windows.Forms.DialogResult.OK:
						Properties.Settings.Default.defaultSavePath = browser.SelectedFolder;
						Properties.Settings.Default.Save();
						break;
				}
			}

			else if (profileDirInitial && armaProfileFound)
			{
				builder.AppendLine("It appears your profile has not been set.");
				builder.AppendLine("Since the default profile exists, it has been set as the default. You can switch to another profiles in the settings.");

				MessageBox.Show(builder.ToString(), "Initial Setup", buttons, image);

				Properties.Settings.Default.profileDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\ArmA 3";
				Properties.Settings.Default.Save();
			}

			else if (!Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\ArmA 3"))
			{
				image = MessageBoxIcon.Warning;
				MessageBox.Show("It appears that you currently do not have ArmA 3 installed." + "\n" + "The go to profile button has been disabled.", "Warning", buttons, image);
				ArmaDir_Button.IsEnabled = false;
			}

			// Always stays the same, so set as early as possible
			fadeOutAnim.From = 1.0;
			fadeOutAnim.To = 0.0;
			fadeOutAnim.FillBehavior = FillBehavior.Stop;

			Utility_Label.Text = labelEmpty;
		}

		private void ActivateEditingMode()
		{
			editing = true;

			ResetElements(true, true, false, true);

			SetUtilityLabel(editingMode);

			RegEvent_Button.Content = defaultSave;

			eventList.IsEnabled = false;
			DelEvent_Button.IsEnabled = false;
			Generate_Button.IsEnabled = false;
			Clipboard_Button.IsEnabled = false;
		}

		private void DeactiveEditingMode()
		{
			editing = false;
			ResetElements(true, true, false, true);

			RegEvent_Button.Content = createAction;

			eventList.IsEnabled = true;
			DelEvent_Button.IsEnabled = true;
			Generate_Button.IsEnabled = true;
			Clipboard_Button.IsEnabled = true;
		}

		private int ValidateAction()
		{
			int returnInt = 0;
			bool validationFail = false;

			if (targetText.Text == "")
			{
				validationFail = true;
				targetText.Background = Brushes.Salmon;
			}
			else
			{
				targetText.Background = Brushes.Transparent;
			}

			bool functionNameValidation = ValidateFunctionName();

			if (functionText.Text == "" || functionNameValidation)
			{
				validationFail = true;
				functionText.Background = Brushes.Salmon;
			}
			else
			{
				functionText.Background = Brushes.Transparent;
			}

			if (eventLabelText.Text == "")
			{
				validationFail = true;
				eventLabelText.Background = Brushes.Salmon;
			}
			else
			{
				eventLabelText.Background = Brushes.Transparent;
			}


			//
			// Progress dependant
			//

			if (progressCheck.IsChecked == true && actionText.Text == "")
			{
				validationFail = true;
				actionText.Background = Brushes.Salmon;
			}
			else
			{
				actionText.Background = Brushes.Transparent;
			}

			if (progressCheck.IsChecked == true && durationText.Text == "")
			{
				validationFail = true;
				durationText.Background = Brushes.Salmon;
			}
			else
			{
				durationText.Background = Brushes.Transparent;
			}

			if (validationFail)
			{
				returnInt++;
			}
			if (functionNameValidation)
			{
				returnInt++;
			}

			return returnInt;
		}

		private bool ValidateFunctionName()
		{
			bool alphanumericFail;
			bool startFail;

			Regex regex = new Regex("([^_0-9a-zA-Z])+");
			alphanumericFail = regex.IsMatch(functionText.Text);

			regex = new Regex("^[0-9]");
			startFail = regex.IsMatch(functionText.Text);

			if (alphanumericFail || startFail)
			{
				return true;
			}

			return false;
		}

		private void SaveButtonHelper()
		{
			if (fileOpen)
			{
				SaveProject();
			}
			else
			{
				SaveAs();
			}
		}

		private void SaveProject()
		{
			string json = GetJSONs();
			string file = openedFile;

			File.WriteAllText(file, json);
			DeregisterChange();
		}

		private void SaveAs()
		{
			string json = GetJSONs();
			string file;

			SaveFileDialog saveDialog = new SaveFileDialog
			{
				AddExtension = true,
				DefaultExt = ".ezp",
				Filter = "EZ-Action Projects (*.ezp)|*.ezp",
				InitialDirectory = Properties.Settings.Default.defaultSavePath
			};

			if (saveDialog.ShowDialog() == true)
			{
				file = saveDialog.FileName;

				this.Title = SetNewTitle(file);

				openedFile = file;
				fileOpen = true;

				File.WriteAllText(file, json);

				DeregisterChange(); // since changes have been saved
			}
		}

		private void SetUtilityLabel(string labelToSet, bool fadeOut = false, int fadeOffset = 1600, int fadeTime = 150)
		{
			// If animPlay is still active and hasn't been removed, do so
			if (animPlay != null)
			{
				animPlay.Stop(Utility_Label);

				Utility_Label.Opacity = (double)1.0;

				Utility_Label.Visibility = Visibility.Visible;
			}

			Utility_Label.Text = labelToSet;
			Utility_Label.Visibility = Visibility.Visible;

			Utility_Label.Opacity = (double)1.0;

			if (fadeOut)
			{
				animPlay = new Storyboard();

				Utility_Label.Opacity = (double)1.0;

				fadeOutAnim.BeginTime = TimeSpan.FromMilliseconds(fadeOffset);
				fadeOutAnim.Duration = new Duration(TimeSpan.FromMilliseconds((double)fadeTime));

				animPlay.Children.Add(fadeOutAnim);
				//Storyboard.SetTarget(fadeOutAnim, Utility_Label);
				Storyboard.SetTargetProperty(fadeOutAnim, new PropertyPath(TextBlock.OpacityProperty));

				animPlay.Completed += CompletedHandler;
				animPlay.Begin(Utility_Label, HandoffBehavior.SnapshotAndReplace, true);
			}
		}



		private string SetNewTitle(string file, bool changed = false)
		{
			if (changed)
			{
				return String.Concat(windowTitleBase, ": *", file);
			}
			else
			{
				return String.Concat(windowTitleBase, ": ", file);
			}

		}

		private void ResetElements(bool colouring, bool texts = false, bool target = false, bool label = false, bool buttonVisible = false)
		{
			if (colouring)
			{
				targetText.Background = Brushes.Transparent;
				functionText.Background = Brushes.Transparent;
				eventLabelText.Background = Brushes.Transparent;
				actionText.Background = Brushes.Transparent;
				durationText.Background = Brushes.Transparent;
			}

			if (texts)
			{
				functionText.Text = "";

				classCheck.IsChecked = false;

				actionText.Text = "";
				eventLabelText.Text = "";

				progressCheck.IsChecked = true;
				durationText.Text = "10";

				RegEvent_Button.Content = createAction;
			}

			if (target)
			{
				targetText.Text = "";
			}

			if (label)
			{
				SetUtilityLabel(labelEmpty, true, 1, 1);
			}

			AdjustEditVisibility(buttonVisible);
		}

		private string GetJSONs()
		{
			Metadata Data = new Metadata();
			String Data_JSON = Data.ExportMetadata();

			string actions = AceEvent.ExportJSONString();

			return String.Concat(Data_JSON, Environment.NewLine, actions);
		}

		private void RegisterChange()
		{
			edited = true;
			this.Title = SetNewTitle(openedFile, true);
		}

		private void DeregisterChange()
		{
			edited = false;
			this.Title = SetNewTitle(openedFile);
		}

		private void AdjustEditVisibility(bool visible)
		{
			switch (visible)
			{
				case true:
					CancelEdit_Button.Visibility = Visibility.Visible;
					break;
				case false:
					CancelEdit_Button.Visibility = Visibility.Hidden;
					break;
			}
		}

		private void ResetWindow()
		{
			this.Title = windowTitleBase;
			openedFile = "";

			DeregisterChange();

			Utility_Label.Text = labelEmpty;

			editing = false;
			fileOpen = false;

			DeactiveEditingMode();

			AceEvent.ResetList();

			eventList.Items.Clear();

			ResetElements(true, true, false, true);
		}

		/* Event Section
		*---------------------------------------------------------------------------*
		*---------------------------------------------------------------------------*/

		// Toolbar Events

		private void New_File_Click(object sender, RoutedEventArgs e)
		{
			ResetWindow();
		}

		private void OpenFile_Button_Click(object sender, RoutedEventArgs e)
		{
			//Flag to indicate the program ran
			bool ran = false;
			bool errorAction = false;

			int index = 0;

			OpenFileDialog file = new OpenFileDialog
			{
				Filter = "EZ-Action Projects (*.ezp)|*.ezp",
				InitialDirectory = Properties.Settings.Default.defaultSavePath
			};

			if (file.ShowDialog() == true)
			{
				ResetWindow();

				fileOpen = true;
				openedFile = file.FileName;

				this.Title = SetNewTitle(openedFile);

				StreamReader readingFile = new StreamReader(file.FileName);

				string line;
				bool first = true;
				while ((line = readingFile.ReadLine()) != null)
				{
					ran = true;

					if (first)
					{
						Metadata data = JsonSerializer.Deserialize<Metadata>(line);

						if (!data.ValidateSelf())
						{
							MessageBox.Show("The metadata couldn't be loaded!");
						}

						first = false;
					}
					else
					{
						AceEvent deserializedEvent = JsonSerializer.Deserialize<TestAppWPF.AceEvent>(line);

						if (deserializedEvent.ValidateSelf())
						{
							AceEvent.eventList.Add(deserializedEvent);

							string concated = String.Concat(deserializedEvent.FunctionName, "on object ", deserializedEvent.TargetEntity);
							eventList.Items.Add(concated);
						}
						else
						{
							MessageBox.Show("Action " + index + " was incorrect and couldn't be imported.");
						}
					}

					index++;
				}

				readingFile.Close();

				if (errorAction)
				{
					MessageBox.Show("At least one error occured during the import of the actions.");
				}

				if (!ran)
				{
					MessageBox.Show("The file was empty or couldn't be loaded!");
				}
			}
		}

		private void Save_Button_Click(object sender, RoutedEventArgs e)
		{
			SaveButtonHelper();
		}

		private void SaveAs_Button_Click(object sender, RoutedEventArgs e)
		{
			SaveAs();
		}

		private void SettingsButton_Click(object sender, RoutedEventArgs e)
		{
			Window1 win1 = new Window1();
			win1.Show();
		}

		private void ArmaDir_Button_Click(object sender, RoutedEventArgs e)
		{
			Process.Start(Properties.Settings.Default.profileDir);
		}

		private void ExportDir_Button_Click(object sender, RoutedEventArgs e)
		{
			Process.Start(Properties.Settings.Default.defaultSavePath);
		}

		// Utility Events
		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (edited)
			{
				MessageBoxButtons buttons = MessageBoxButtons.YesNoCancel;
				MessageBoxIcon image = MessageBoxIcon.Warning;
				DialogResult result;

				result = MessageBox.Show("You have unsaved changes. Save before exiting? ", "Unsaved changes", buttons, image);

				switch (result)
				{
					case System.Windows.Forms.DialogResult.Cancel:
						e.Cancel = true;
						break;
					case System.Windows.Forms.DialogResult.Yes:
						SaveButtonHelper();
						break;
					case System.Windows.Forms.DialogResult.No:
						break;
					default:
						break;
				}
			}
		}

		private void CompletedHandler(object sender, EventArgs e)
		{
			animPlay.Stop(Utility_Label);
			Utility_Label.Opacity = 0.0;
		}

		private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
		{
			Regex regex = new Regex("[^0-9]+");
			e.Handled = regex.IsMatch(e.Text);
		}

		// Main Window Events
		private void CancelEdit_Button_Click(object sender, RoutedEventArgs e)
		{
			DeactiveEditingMode();

			SetUtilityLabel(editCancelled, true);

			// Reset all the UI elements
			ResetElements(true, true);
		}

		private void RegEvent_Button_Click(object sender, RoutedEventArgs e)
		{
			int invalidationReturn = ValidateAction();

			// Action Validation
			if (invalidationReturn != 0)
			{
				switch (invalidationReturn)
				{
					case 1:
						SetUtilityLabel(validationError);
						break;
					case 2:
						SetUtilityLabel(nameValidationError);
						break;
				}
				return;
			}

			string concated = String.Concat(functionText.Text, " for object ", targetText.Text);
			int intDuration = Convert.ToInt32(durationText.Text);

			RegisterChange();

			// When editing, delete previous index and insert action at previous index
			if (editing)
			{
				int indexCache = eventList.SelectedIndex;
				eventList.Items.RemoveAt(indexCache);

				eventList.Items.Insert(indexCache, concated);

				AceEvent.eventList[indexCache] = new AceEvent(functionText.Text, actionText.Text, progressCheck.IsChecked, intDuration, targetText.Text, classCheck.IsChecked, eventLabelText.Text);

				targetText.Text = targetCache;
				targetCache = "";

				SetUtilityLabel(actionEdited, true);

				DeactiveEditingMode();
			}
			// When adding, can just straight add and create object
			else
			{
				AceEvent newEvent = new AceEvent(functionText.Text, actionText.Text, progressCheck.IsChecked, intDuration, targetText.Text, classCheck.IsChecked, eventLabelText.Text);
				AceEvent.eventList.Add(newEvent);

				eventList.Items.Add(concated);

				SetUtilityLabel(actionCreated, true);
			}

			// Reset all the UI elements
			ResetElements(true, true);
		}

		private void EventList_MouseDoubleClick(object sender, RoutedEventArgs e)
		{
			try
			{
				object selectedItem = eventList.SelectedItem;
				int index = eventList.Items.IndexOf(selectedItem);

				AceEvent editAction = AceEvent.eventList[index];

				ActivateEditingMode();

				//Disable the event list so only one thing can be edited at the same time
				eventList.IsEnabled = false;

				//If we're here, everything worked out, so let's get cracking
				targetCache = targetText.Text;

				targetText.Text = editAction.TargetEntity;
				classCheck.IsChecked = editAction.ClassCheck;

				functionText.Text = editAction.FunctionName;
				eventLabelText.Text = editAction.ActionLabel;

				progressCheck.IsChecked = editAction.ProgressBar;
				actionText.Text = editAction.DisplayText;
				durationText.Text = editAction.Duration.ToString();
			}
			// Catch OutOfRangeExceptions when the user clicks a non-existing item
			catch (System.ArgumentOutOfRangeException)
			{
				editing = false;
				DeactiveEditingMode();
				return;
			}
		}

		private void DelEvent_Button_Click(object sender, RoutedEventArgs e)
		{
			object selectedItem = eventList.SelectedItem;

			if (eventList.Items.Count == 0)
			{
				SetUtilityLabel(noActionExists, true);
				return;
			}
			if (selectedItem != null)
			{
				AceEvent.eventList.RemoveAt(eventList.Items.IndexOf(selectedItem));

				eventList.Items.Remove(selectedItem);

				RegisterChange();

				SetUtilityLabel(actionDeleted, true);
			}
			else
			{
				SetUtilityLabel(noActionSelected, true);
			}
		}

		private void Generate_Button_Click(object sender, RoutedEventArgs e)
		{
			//Return codes:
			//0 - success
			//1 - user abort
			//2 - nothing to declare
			//3 - other error
			int evtBuild = AceEvent.BuildSQF();

			switch (evtBuild)
			{
				case 0:
					SetUtilityLabel(actionsWritten, true);
					break;
				case 1:
					SetUtilityLabel(writeAborted, true);
					break;
				case 2:
					SetUtilityLabel(noActionsToWrite, true);
					break;
				case 3:
					SetUtilityLabel(errorWrite, true);
					break;
			}
		}

		private void Clipboard_Button_Click(object sender, RoutedEventArgs e)
		{
			int result;
			result = AceEvent.BuildToClipboard();

			switch (result)
			{
				case 0:
					SetUtilityLabel(copiedToClipboard, true);
					break;
				case 1:
					SetUtilityLabel(noActionsToWrite, true);
					break;
				case 2:
					SetUtilityLabel(errorWrite, true);
					break;
			}
		}

		private void OpenVarPage_Button_Click(object sender, RoutedEventArgs e)
		{

		}
	}
}


/* The officially sanctioned graveyard
* Stuff that's not implemented but will be (mostly variable stuff) goes here
*---------------------------------------------------------------------------*
private void VarDel_Button_Click(object sender, RoutedEventArgs e)
		{
			object selectedItem = variableList.SelectedItem;

			if (selectedItem != null)
			{
				variableContent.v ariableList.RemoveAt(variableList.Items.IndexOf(selectedItem));

				variableList.Items.Remove(selectedItem);
			}
		}
 
 		private void AddVar_Button_Click(object sender, RoutedEventArgs e)
{
	if (variableText != null)
	{
		string concat = String.Concat("Variable: ", variableText.Text, "; Value: ", variableValue.Text);
		variableList.Items.Add(concat);

		variableContent var = new variableContent(variableText.Text, variableValue.Text);
		variableContent.variableList.Add(var);

		variableText.Text = "";
		variableValue.Text = "";
	}

*/