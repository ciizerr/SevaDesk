using System;
using System.IO;
using Microsoft.UI.Xaml.Controls;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;

namespace SevaDesk_App.Views.Dialogs;

public sealed partial class WalkInCompleteSessionDialog : ContentDialog
{
    public bool DeleteTemporaryFiles => ChkDeleteFiles.IsChecked == true;

    public WalkInCompleteSessionDialog()
    {
        InitializeComponent();
        this.EnableLightDismiss();

        try
        {
            var folder = AppServices.FolderManager.GetWalkInTempFolderPath();
            if (Directory.Exists(folder))
            {
                var files = Directory.GetFiles(folder);
                TxtFilesSummary.Text = files.Length switch
                {
                    0 => "0 files in today's Walk-in temp folder",
                    1 => "1 temporary file in today's Walk-in folder",
                    _ => $"{files.Length} temporary files in today's Walk-in folder"
                };

                if (files.Length == 0)
                {
                    ChkDeleteFiles.IsChecked = false;
                    ChkDeleteFiles.IsEnabled = false;
                }
            }
            else
            {
                TxtFilesSummary.Text = "No temporary files in today's folder";
                ChkDeleteFiles.IsChecked = false;
                ChkDeleteFiles.IsEnabled = false;
            }
        }
        catch
        {
            TxtFilesSummary.Text = "Temporary folder ready for cleanup";
        }
    }
}
