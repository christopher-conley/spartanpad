namespace Spartanpad.Core;

/// <summary>
/// The user-facing texts of the reference (the local reference notes §3) with "Notepad" replaced by
/// the application name. <c>{0}</c> is the file name where the reference used <c>%%</c> or <c>%1</c>.
/// </summary>
public static class ReferenceStrings
{
    public const string ApplicationName = "Spartanpad";

    // String table 1
    public const string CannotOpen = "Cannot open the {0} file.\n\nMake sure a disk is in the drive you specified.";
    public const string CannotFindCreate = "Cannot find the {0} file.\n\nDo you want to create a new file?";
    public const string CannotFind = "Cannot find \"{0}\"";
    public const string OutOfMemory = "Not enough memory available to complete this operation. Quit one or more applications to increase available memory, and then try again.";
    public const string FileTooLarge = "The {0} file is too large for " + ApplicationName + ".\n\nUse another editor to edit the file.";
    public const string NotValidFileName = "Not a valid file name.";
    public const string CannotCreate = "Cannot create the {0} file.\n\nMake sure that the path and file name are correct.";
    public const string WordWrapTooMuchText = "Cannot carry out the Word Wrap command because there is too much text in the file.";

    // String table 2
    public const string FilterTextDocuments = "Text Documents (*.txt)";
    public const string FilterAllFiles = "All Files";
    public const string OpenTitle = "Open";
    public const string SaveAsTitle = "Save As";
    public const string NoPermission = "{0}\nYou do not have permission to open this file.  See the owner of the file or an administrator to obtain permission.";
    public const string AnsiLossWarning = "{0}\n This file contains characters in Unicode format which will be lost if you save this file as an ANSI encoded text file. To keep the Unicode information, click Cancel below and then select one of the Unicode options from the Encoding drop down list. Continue?";
    public const string GoToTitle = ApplicationName + " - Goto Line";
    public const string LineBeyondEnd = "The line number is beyond the total number of lines";

    // String table 3 and 4
    public const string FoundFromBottom = " Found next from the bottom";
    public const string FoundFromTop = " Found next from the top";
    public const string EncodingLabel = "&Encoding:";
    public const string AutoDetect = "Auto-Detect";

    // String table 188
    public const string SaveChangesQuestion = "Do you want to save changes to {0}?";
    public const string SaveButton = "Save";
    public const string DontSaveButton = "Don't Save";
    public const string CancelButton = "Cancel";

    // Not in the reference: the one feature this build does not have.
    public const string PrintingUnavailable = "Printing is not available in this build of " + ApplicationName + ".\n\nPage Setup settings are kept and will be used when printing is added.";
}
