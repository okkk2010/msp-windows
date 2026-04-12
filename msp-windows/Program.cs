using System;
using System.Windows.Forms;

public class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        RemoteControlForm remoteControl = new RemoteControlForm();
        Application.Run(remoteControl);
    }
}
