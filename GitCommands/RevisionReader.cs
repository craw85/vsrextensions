using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GitExtUtils;
using GitUI;
using GitUIPluginInterfaces;
using JetBrains.Annotations;
using Microsoft.VisualStudio.Threading;
using Versionr;

namespace GitCommands
{
    [Flags]
    public enum RefFilterOptions
    {
        Branches = 1,              // --branches
        Remotes = 2,               // --remotes
        Tags = 4,                  // --tags
        Stashes = 8,               //
        All = 15,                  // --all
        Boundary = 16,             // --boundary
        ShowGitNotes = 32,         // --not --glob=notes --not
        NoMerges = 64,             // --no-merges
        FirstParent = 128,         // --first-parent
        SimplifyByDecoration = 256 // --simplify-by-decoration
    }

    public sealed class RevisionReader : IDisposable
    {
        private const string EndOfBody = "1DEA7CC4-FB39-450A-8DDF-762FCEA28B05";
        private const string FullFormat =

              // These header entries can all be decoded from the bytes directly.
              // Each hash is 20 bytes long.

              /* Object ID       */ "%H" +
              /* Tree ID         */ "%T" +
              /* Parent IDs      */ "%P%n" +
              /* Author date     */ "%at%n" +
              /* Commit date     */ "%ct%n" +
              /* Encoding        */ "%e%n" +
              /*
               Items below here must be decoded as strings to support non-ASCII.
               */
              /* Author name     */ "%aN%n" +
              /* Author email    */ "%aE%n" +
              /* Committer name  */ "%cN%n" +
              /* Committer email */ "%cE%n" +
              /* Commit subject  */ "%s%n%n" +
              /* Commit body     */ "%b" + EndOfBody;

        private readonly CancellationTokenSequence _cancellationTokenSequence = new CancellationTokenSequence();

        public void Execute(
            VsrModule module,
            IReadOnlyList<IGitRef> refs,
            IObserver<GitRevision> subject,
            RefFilterOptions refFilterOptions,
            string branchFilter,
            string revisionFilter,
            string pathFilter,
            [CanBeNull] Func<GitRevision, bool> revisionPredicate)
        {
            ThreadHelper.JoinableTaskFactory
                .RunAsync(() => ExecuteAsync(module, refs, subject, refFilterOptions, branchFilter, revisionFilter, pathFilter, revisionPredicate))
                .FileAndForget(
                    ex =>
                    {
                        subject.OnError(ex);
                        return false;
                    });
        }

        private async Task ExecuteAsync(
            VsrModule module,
            IReadOnlyList<IGitRef> refs,
            IObserver<GitRevision> subject,
            RefFilterOptions refFilterOptions,
            string branchFilter,
            string revisionFilter,
            string pathFilter,
            [CanBeNull] Func<GitRevision, bool> revisionPredicate)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var token = _cancellationTokenSequence.Next();

            var revisionCount = 0;

            await TaskScheduler.Default;

            token.ThrowIfCancellationRequested();

            var branchName = module.IsValidVersionrWorkingDir()
                ? module.GetSelectedBranch()
                : "";

            token.ThrowIfCancellationRequested();

            UpdateSelectedRef(module, refs, branchName);
            var refsByObjectId = refs.ToLookup(head => head.ObjectId);

            token.ThrowIfCancellationRequested();

            var arguments = BuildArguments(refFilterOptions, branchFilter, revisionFilter, pathFilter);

#if TRACE
            var sw = Stopwatch.StartNew();
#endif

            var versions = module.GetLog(1000); // TODO: VSR - limit is in revisionFilter variable

            foreach (var version in versions)
            {
                token.ThrowIfCancellationRequested();

                var revision = new GitRevision(new ObjectId(version.ID))
                {
                    ParentIds = version.Parent.HasValue ? new[] { new ObjectId(version.Parent.Value) } : null,
                    TreeGuid = null, // TODO: VSR
                    Author = version.Author,
                    AuthorEmail = version.Email,
                    AuthorDate = version.Timestamp, // TODO: VSR
                    Committer = version.Author, // TODO: VSR
                    CommitterEmail = version.Email, // TODO: VSR
                    CommitDate = version.Timestamp,
                    MessageEncoding = null, // TODO: VSR
                    Subject = version.Message,
                    Body = version.Message, // TODO: VSR
                    Name = version.ShortName, // TODO: VSR
                    HasMultiLineMessage = false, // TODO: VSR - !ReferenceEquals(Subject, Body),
                    HasNotes = false
                };

                if (revisionPredicate == null || revisionPredicate(revision))
                {
                    // The full commit message body is used initially in InMemFilter, after which it isn't
                    // strictly needed and can be re-populated asynchronously.
                    //
                    // We keep full multiline message bodies within the last six months.
                    // Commits earlier than that have their properties set to null and the
                    // memory will be GCd.
                    if (DateTime.Now - revision.AuthorDate > TimeSpan.FromDays(30 * 6))
                    {
                        revision.Body = null;
                    }

                    // Look up any refs associated with this revision
                    revision.Refs = refsByObjectId[revision.ObjectId].AsReadOnlyList();

                    revisionCount++;

                    subject.OnNext(revision);
                }
            }

            // This property is relatively expensive to call for every revision, so
            // cache it for the duration of the loop.
            var logOutputEncoding = module.LogOutputEncoding;

            if (!token.IsCancellationRequested)
            {
                subject.OnCompleted();
            }
        }

        private ArgumentBuilder BuildArguments(RefFilterOptions refFilterOptions,
            string branchFilter,
            string revisionFilter,
            string pathFilter)
        {
            return new GitArgumentBuilder("log")
            {
                "-z",
                branchFilter,
                $"--pretty=format:\"{FullFormat}\"",
                {
                    refFilterOptions.HasFlag(RefFilterOptions.FirstParent),
                    "--first-parent",
                    new ArgumentBuilder
                    {
                        { AppSettings.ShowReflogReferences, "--reflog" },
                        { AppSettings.SortByAuthorDate, "--author-date-order" },
                        {
                            refFilterOptions.HasFlag(RefFilterOptions.All),
                            "--all",
                            new ArgumentBuilder
                            {
                                {
                                    refFilterOptions.HasFlag(RefFilterOptions.Branches) &&
                                    !string.IsNullOrWhiteSpace(branchFilter) && branchFilter.IndexOfAny(new[] { '?', '*', '[' }) != -1,
                                    "--branches=" + branchFilter
                                },
                                { refFilterOptions.HasFlag(RefFilterOptions.Remotes), "--remotes" },
                                { refFilterOptions.HasFlag(RefFilterOptions.Tags), "--tags" },
                            }.ToString()
                        },
                        { refFilterOptions.HasFlag(RefFilterOptions.Boundary), "--boundary" },
                        { refFilterOptions.HasFlag(RefFilterOptions.ShowGitNotes), "--not --glob=notes --not" },
                        { refFilterOptions.HasFlag(RefFilterOptions.NoMerges), "--no-merges" },
                        { refFilterOptions.HasFlag(RefFilterOptions.SimplifyByDecoration), "--simplify-by-decoration" }
                    }.ToString()
                },
                revisionFilter,
                "--",
                pathFilter
            };
        }

        private static void UpdateSelectedRef(VsrModule module, IReadOnlyList<IGitRef> refs, string branchName)
        {
            var selectedRef = refs.FirstOrDefault(head => head.Name == branchName);

            if (selectedRef != null)
            {
                selectedRef.IsSelected = true;

                var localConfigFile = module.LocalConfigFile;
                var selectedHeadMergeSource = refs.FirstOrDefault(
                    head => head.IsRemote
                         && selectedRef.GetTrackingRemote(localConfigFile) == head.Remote
                         && selectedRef.GetMergeWith(localConfigFile) == head.LocalName);

                if (selectedHeadMergeSource != null)
                {
                    selectedHeadMergeSource.IsSelectedHeadMergeSource = true;
                }
            }
        }

        [CanBeNull]
        private static (string body, string additionalData) ParseCommitBody([NotNull] StringLineReader reader, [NotNull] string subject)
        {
            int lengthOfSubjectRepeatedInBody = subject.Length + 2/*newlines*/;
            if (reader.Remaining == lengthOfSubjectRepeatedInBody + EndOfBody.Length)
            {
                return (body: subject, additionalData: null);
            }

            string tail = reader.ReadToEnd() ?? "";
            int indexOfEndOfBody = tail.LastIndexOf(EndOfBody, StringComparison.InvariantCulture);
            if (indexOfEndOfBody < 0)
            {
                // TODO log this parse error
                Debug.Fail("Missing end-of-body marker in the log -- this should not happen");
                return (body: null, additionalData: null);
            }

            string additionalData = null;
            if (tail.Length > indexOfEndOfBody + EndOfBody.Length)
            {
                additionalData = tail.Substring(indexOfEndOfBody + EndOfBody.Length).TrimStart();
            }

            string body = indexOfEndOfBody == lengthOfSubjectRepeatedInBody
                          ? subject : tail.Substring(0, indexOfEndOfBody).TrimEnd();
            return (body, additionalData);
        }

        public void Dispose()
        {
            _cancellationTokenSequence.Dispose();
        }

        #region Nested type: StringLineReader

        /// <summary>
        /// Simple type to walk along a string, line by line, without redundant allocations.
        /// </summary>
        internal struct StringLineReader
        {
            private readonly string _s;
            private int _index;

            public StringLineReader(string s)
            {
                _s = s;
                _index = 0;
            }

            public int Remaining => _s.Length - _index;

            [CanBeNull]
            public string ReadLine([CanBeNull] StringPool pool = null, bool advance = true)
            {
                if (_index == _s.Length)
                {
                    return null;
                }

                var startIndex = _index;
                var endIndex = _s.IndexOf('\n', startIndex);

                if (endIndex == -1)
                {
                    return ReadToEnd(advance);
                }

                if (advance)
                {
                    _index = endIndex + 1;
                }

                return pool != null
                    ? pool.Intern(_s, startIndex, endIndex - startIndex)
                    : _s.Substring(startIndex, endIndex - startIndex);
            }

            [CanBeNull]
            public string ReadToEnd(bool advance = true)
            {
                if (_index == _s.Length)
                {
                    return null;
                }

                var s = _s.Substring(_index);

                if (advance)
                {
                    _index = _s.Length;
                }

                return s;
            }
        }

        #endregion

        internal TestAccessor GetTestAccessor()
            => new TestAccessor(this);

        internal readonly struct TestAccessor
        {
            private readonly RevisionReader _revisionReader;

            internal TestAccessor(RevisionReader revisionReader)
            {
                _revisionReader = revisionReader;
            }

            internal ArgumentBuilder BuildArgumentsBuildArguments(RefFilterOptions refFilterOptions,
                string branchFilter, string revisionFilter, string pathFilter) =>
                _revisionReader.BuildArguments(refFilterOptions, branchFilter, revisionFilter, pathFilter);

            internal static (string body, string additionalData) ParseCommitBody(StringLineReader reader, string subject) =>
                RevisionReader.ParseCommitBody(reader, subject);

            internal static StringLineReader MakeReader(string s) => new StringLineReader(s);

            internal static string EndOfBody => RevisionReader.EndOfBody;
        }
    }
}
