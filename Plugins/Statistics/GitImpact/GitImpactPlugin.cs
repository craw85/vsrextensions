using System.ComponentModel.Composition;
using GitImpact.Properties;
using GitUIPluginInterfaces;
using ResourceManager;

namespace GitImpact
{
    [Export(typeof(IGitPlugin))]
    public class GitImpactPlugin : GitPluginBase, IGitPluginForRepository
    {
        public GitImpactPlugin() : base(false)
        {
            SetNameAndDescription("Impact Graph");
            Translate();
            Icon = Resources.IconGitImpact;
        }

        #region IGitPlugin Members

        public override bool Execute(GitUIEventArgs args)
        {
            if (string.IsNullOrEmpty(args.VsrModule.WorkingDir))
            {
                return false;
            }

            using (var form = new FormImpact(args.VsrModule))
            {
                form.ShowDialog(args.OwnerForm);
            }

            return false;
        }

        #endregion
    }
}
