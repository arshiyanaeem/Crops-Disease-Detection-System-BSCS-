Imports System
Imports System.Windows.Forms

Namespace AgriExpertSystem

    Module Program

        ''' <summary>
        ''' Main entry point of the AXPERT-Mini application.
        ''' </summary>
        <STAThread>
        Sub Main()
            Application.SetHighDpiMode(HighDpiMode.SystemAware)
            Application.EnableVisualStyles()
            Application.SetCompatibleTextRenderingDefault(False)
            Application.Run(New Form1())
        End Sub

    End Module

End Namespace
