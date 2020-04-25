﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Atlassian.Jira;
using GitExtUtils.GitUI;
using GitUI;
using GitUIPluginInterfaces;
using GitUIPluginInterfaces.UserControls;
using JiraCommitHintPlugin.Properties;
using NString;
using ResourceManager;

namespace JiraCommitHintPlugin
{
    [Export(typeof(IGitPlugin))]
    public class JiraCommitHintPlugin : GitPluginBase, IGitPluginForRepository
    {
        private static readonly TranslationString JiraFieldsLabel = new TranslationString("Jira fields");
        private static readonly TranslationString QueryHelperLinkText = new TranslationString("Open the query helper inside Jira");
        private static readonly TranslationString InvalidUrlMessage = new TranslationString("A valid url is required to launch the Jira query helper");
        private static readonly TranslationString InvalidUrlCaption = new TranslationString("Invalid Jira url");
        private static readonly TranslationString PreviewButtonText = new TranslationString("Preview");
        private static readonly TranslationString QueryHelperOpenErrorText = new TranslationString("Unable to open Jira query helper");
        private static readonly TranslationString EmptyQueryResultMessage = new TranslationString("[Empty Jira Query Result]");
        private static readonly TranslationString EmptyQueryResultCaption = new TranslationString("First Task Preview");

        private const string DefaultFormat = "{Key} {Summary}";
        private Jira _jira;
        private string _query;
        private string _stringTemplate = DefaultFormat;
        private readonly BoolSetting _enabledSettings = new BoolSetting("Jira hint plugin enabled", false);
        private readonly StringSetting _urlSettings = new StringSetting("Jira URL", @"https://jira.atlassian.com");
        private readonly CredentialsSetting _credentialsSettings;

        // For compatibility reason, the setting key is kept to "JDL Query" even if the label is, rightly, "JQL Query" (for "Jira Query Language")
        private readonly StringSetting _jqlQuerySettings = new StringSetting("JDL Query", "JQL Query", "assignee = currentUser() and resolution is EMPTY ORDER BY updatedDate DESC", true);
        private readonly StringSetting _stringTemplateSetting = new StringSetting("Jira Message Template", "Message Template", DefaultFormat, true);
        private readonly string _jiraFields = $"{{{string.Join("} {", typeof(Issue).GetProperties().Where(i => i.CanRead).Select(i => i.Name).OrderBy(i => i).ToArray())}}}";
        private IVsrModule _gitModule;
        private JiraTaskDTO[] _currentMessages;
        private Button _btnPreview;

        public JiraCommitHintPlugin() : base(true)
        {
            SetNameAndDescription("Jira Commit Hint");
            Translate();
            Icon = Resources.IconJira;

            _credentialsSettings = new CredentialsSetting("JiraCredentials", "Jira credentials", () => _gitModule?.WorkingDir);
        }

        public override bool Execute(GitUIEventArgs args)
        {
            if (!_enabledSettings.ValueOrDefault(Settings))
            {
                args.GitUICommands.StartSettingsDialog(this);
                return false;
            }

            if (_jira == null)
            {
                return false;
            }

            ThreadHelper.JoinableTaskFactory.RunAsync(
                async () =>
                {
                    var message = await GetMessageToCommitAsync(_jira, _query, _stringTemplate);
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    MessageBox.Show(string.Join(Environment.NewLine, message.Select(jt => jt.Text).ToArray()), "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });

            return false;
        }

        public override IEnumerable<ISetting> GetSettings()
        {
            yield return _enabledSettings;

            _urlSettings.CustomControl = new TextBox();
            yield return _urlSettings;

            _credentialsSettings.CustomControl = new CredentialsControl();
            yield return _credentialsSettings;

            _jqlQuerySettings.CustomControl = new TextBox();
            yield return _jqlQuerySettings;

            var queryHelperLink = new LinkLabel { Text = QueryHelperLinkText.Text };
            queryHelperLink.Click += QueryHelperLink_Click;
            yield return new PseudoSetting(queryHelperLink);

            yield return new PseudoSetting(_jiraFields, JiraFieldsLabel.Text, DpiUtil.Scale(55));

            var txtTemplate = new TextBox
            {
                Height = DpiUtil.Scale(75),
                Multiline = true,
                ScrollBars = ScrollBars.Horizontal
            };
            _btnPreview = new Button
            {
                Text = PreviewButtonText.Text,
                Top = DpiUtil.Scale(45),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom
            };
            _btnPreview.Size = DpiUtil.Scale(_btnPreview.Size);
            _btnPreview.Click += btnPreviewClick;
            _btnPreview.Left = txtTemplate.Width - _btnPreview.Width - DpiUtil.Scale(8);
            txtTemplate.Controls.Add(_btnPreview);
            _stringTemplateSetting.CustomControl = txtTemplate;
            yield return _stringTemplateSetting;
        }

        private void QueryHelperLink_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_urlSettings.CustomControl.Text))
            {
                MessageBox.Show(null, InvalidUrlMessage.Text, InvalidUrlCaption.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Process.Start(_urlSettings.CustomControl.Text + "/secure/IssueNavigator.jspa?mode=show&createNew=true");
            }
            catch (Exception ex)
            {
                MessageBox.Show(null, ex.Message, QueryHelperOpenErrorText.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnPreviewClick(object sender, EventArgs eventArgs)
        {
            try
            {
                _btnPreview.Enabled = false;

                var localJira = Jira.CreateRestClient(_urlSettings.CustomControl.Text, _credentialsSettings.CustomControl.UserName,
                    _credentialsSettings.CustomControl.Password);
                var localQuery = _jqlQuerySettings.CustomControl.Text;
                var localStringTemplate = _stringTemplateSetting.CustomControl.Text;

                ThreadHelper.JoinableTaskFactory.RunAsync(
                    async () =>
                    {
                        var message = await GetMessageToCommitAsync(localJira, localQuery, localStringTemplate);
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        var preview = message.FirstOrDefault();

                        MessageBox.Show(null, preview == null ? EmptyQueryResultMessage.Text : preview.Text, EmptyQueryResultCaption.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);

                        _btnPreview.Enabled = true;
                    });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _btnPreview.Enabled = true;
            }
        }

        public override void Register(IGitUICommands gitUiCommands)
        {
            base.Register(gitUiCommands);
            _gitModule = gitUiCommands.VsrModule;
            gitUiCommands.PostSettings += gitUiCommands_PostSettings;
            gitUiCommands.PreCommit += gitUiCommands_PreCommit;
            gitUiCommands.PostCommit += gitUiCommands_PostRepositoryChanged;
            gitUiCommands.PostRepositoryChanged += gitUiCommands_PostRepositoryChanged;
            UpdateJiraSettings();
        }

        private void UpdateJiraSettings()
        {
            if (!_enabledSettings.ValueOrDefault(Settings))
            {
                return;
            }

            var url = _urlSettings.ValueOrDefault(Settings);
            var credentials = _credentialsSettings.GetValueOrDefault(Settings);

            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(credentials.UserName))
            {
                return;
            }

            _jira = Jira.CreateRestClient(url, credentials.UserName, credentials.Password);
            _query = _jqlQuerySettings.ValueOrDefault(Settings);
            _stringTemplate = _stringTemplateSetting.ValueOrDefault(Settings);
            if (_btnPreview == null)
            {
                return;
            }

            _btnPreview.Click -= btnPreviewClick;
            _btnPreview = null;
        }

        private void gitUiCommands_PostSettings(object sender, GitUIPostActionEventArgs e)
        {
            UpdateJiraSettings();
        }

        public override void Unregister(IGitUICommands gitUiCommands)
        {
            base.Unregister(gitUiCommands);
            gitUiCommands.PreCommit -= gitUiCommands_PreCommit;
            gitUiCommands.PostCommit -= gitUiCommands_PostRepositoryChanged;
            gitUiCommands.PostSettings -= gitUiCommands_PostSettings;
            gitUiCommands.PostRepositoryChanged -= gitUiCommands_PostRepositoryChanged;
        }

        private void gitUiCommands_PreCommit(object sender, GitUIEventArgs e)
        {
            if (!_enabledSettings.ValueOrDefault(Settings))
            {
                return;
            }

            if (_jira?.Issues == null)
            {
                return;
            }

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                var currentMessages = await GetMessageToCommitAsync(_jira, _query, _stringTemplate);

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                _currentMessages = currentMessages;
                foreach (var message in _currentMessages)
                {
                    e.GitUICommands.AddCommitTemplate(message.Title, () => message.Text, Icon);
                }
            });
        }

        private void gitUiCommands_PostRepositoryChanged(object sender, GitUIEventArgs e)
        {
            if (!_enabledSettings.ValueOrDefault(Settings))
            {
                return;
            }

            if (_currentMessages == null)
            {
                return;
            }

            foreach (var message in _currentMessages)
            {
                e.GitUICommands.RemoveCommitTemplate(message.Title);
            }

            _currentMessages = null;
        }

        private async Task<JiraTaskDTO[]> GetMessageToCommitAsync(Jira jira, string query, string stringTemplate)
        {
            try
            {
                var results = await jira.Issues.GetIssuesFromJqlAsync(query);
                return results
                    .Select(issue => new JiraTaskDTO(issue.Key + ": " + issue.Summary, StringTemplate.Format(stringTemplate, issue)))
                    .ToArray();
            }
            catch (Exception ex)
            {
                return new[] { new JiraTaskDTO($"{Description} error", ex.ToString()) };
            }
        }

        private class JiraTaskDTO
        {
            public string Title { get; }
            public string Text { get; }

            public JiraTaskDTO(string title, string text)
            {
                Title = title;
                Text = text;
            }
        }
    }
}
