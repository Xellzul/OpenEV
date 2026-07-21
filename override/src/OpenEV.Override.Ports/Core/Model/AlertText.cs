namespace OpenEV.Override.Ports.Core.Model;

// Managed home for the news / alert / mission-text message. The original kept
// it as a C string in a BSS buffer behind the PEF-relocated pointer cell
// 0x10080f6c (PTR_DAT_10080f6c): gameplay events built the text into the buffer
// (CStringCopy/Concat chains)
// and the alert dialogs TETextBox'd it. The message lives here as a real C# string.
public static class AlertText
{
    public static string Message = "";
}
