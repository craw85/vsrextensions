using System;

namespace GitCommands.Git
{
    public sealed class VsrModuleEventArgs : EventArgs
    {
        public VsrModuleEventArgs(VsrModule gitModule)
        {
            VsrModule = gitModule;
        }

        public VsrModule VsrModule { get; }
    }
}
