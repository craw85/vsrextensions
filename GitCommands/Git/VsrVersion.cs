using System;
using System.Collections.Generic;
using System.Linq;

namespace GitCommands
{
    public class VsrVersion : IComparable<VsrVersion>
    {
        public static readonly VsrVersion LastSupportedVersion = new VsrVersion("1.0.0");
        public static readonly VsrVersion LastRecommendedVersion = new VsrVersion("1.2.0");

        private static VsrVersion _current;

        public static VsrVersion Current
        {
            get
            {
                if (_current == null || _current.IsUnknown)
                {
                    var output = new Executable(AppSettings.VsrCommand).GetOutput("--version");
                    _current = new VsrVersion(output);
                }

                return _current;
            }
        }

        public readonly string Full;
        private readonly int _a;
        private readonly int _b;
        private readonly int _c;
        private readonly int _d;

        public VsrVersion(string version)
        {
            Full = Fix();

            var numbers = GetNumbers();
            _a = Get(numbers, 0);
            _b = Get(numbers, 1);
            _c = Get(numbers, 2);
            _d = Get(numbers, 3);

            string Fix()
            {
                string ver = version.Trim();

                if (ver == null)
                {
                    return "";
                }

                const string Prefix = "Versionr v";

                if (ver.StartsWith(Prefix))
                {
                    ver = ver.Substring(Prefix.Length);
                }

                int nl = ver.IndexOfAny(new char[] { ' ', '\n' });
                if (nl >= 0)
                {
                    ver = ver.Substring(0, nl);
                }

                return ver.Trim();
            }

            IReadOnlyList<int> GetNumbers()
            {
                return ParseNumbers().ToList();

                IEnumerable<int> ParseNumbers()
                {
                    foreach (var number in Full.Split('.'))
                    {
                        if (int.TryParse(number, out var value))
                        {
                            yield return value;
                        }
                    }
                }
            }

            int Get(IReadOnlyList<int> values, int index)
            {
                return index < values.Count ? values[index] : 0;
            }
        }

        public bool FetchCanAskForProgress => true;

        public bool LogFormatRecodesCommitMessage => true;

        public bool PushCanAskForProgress => true;

        public bool StashUntrackedFilesSupported => true;

        public bool SupportPushWithRecursiveSubmodulesCheck => true;

        public bool SupportPushWithRecursiveSubmodulesOnDemand => true;

        public bool SupportPushForceWithLease => true;

        public bool RaceConditionWhenGitStatusIsUpdatingIndex => true;

        public bool SupportAheadBehindData => true;

        public bool SupportWorktree => true;

        public bool SupportWorktreeList => true;

        public bool SupportMergeUnrelatedHistory => true;

        public bool SupportStatusPorcelainV2 => true;

        public bool DepreciatedLfsClone => true;

        public bool SupportNoOptionalLocks => true;

        public bool SupportRebaseMerges => true;

        public bool SupportGuiMergeTool => true;

        public bool IsUnknown => _a == 0 && _b == 0 && _c == 0 && _d == 0;

        // Returns true if it's possible to pass given string as command line
        // argument to git for searching.
        // As of msysgit 1.7.3.1 git-rev-list requires its search arguments
        // (--author, --committer, --regex) to be encoded with the exact encoding
        // used at commit time.
        // This causes problems under Windows, where command line arguments are
        // passed as WideChars. Git uses argv, which contains strings
        // recoded into 8-bit system codepage, and that means searching for strings
        // outside ASCII range gets crippled, unless commit messages in git
        // are encoded according to system codepage.
        // For versions of git displaying such behaviour, this function should return
        // false if its argument isn't command-line safe, i.e. it contains chars
        // outside ASCII (7bit) range.
        public bool IsRegExStringCmdPassable(string s)
        {
            if (s == null)
            {
                return true;
            }

            foreach (char ch in s)
            {
                if ((uint)ch >= 0x80)
                {
                    return false;
                }
            }

            return true;
        }

        private static int Compare(VsrVersion left, VsrVersion right)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (right == null)
            {
                return 1;
            }

            if (left == null)
            {
                return -1;
            }

            int compareA = left._a.CompareTo(right._a);
            if (compareA != 0)
            {
                return compareA;
            }

            int compareB = left._b.CompareTo(right._b);
            if (compareB != 0)
            {
                return compareB;
            }

            int compareC = left._c.CompareTo(right._c);
            if (compareC != 0)
            {
                return compareC;
            }

            return left._d.CompareTo(right._d);
        }

        public int CompareTo(VsrVersion other) => Compare(this, other);

        public static bool operator >(VsrVersion left, VsrVersion right) => Compare(left, right) > 0;
        public static bool operator <(VsrVersion left, VsrVersion right) => Compare(left, right) < 0;
        public static bool operator >=(VsrVersion left, VsrVersion right) => Compare(left, right) >= 0;
        public static bool operator <=(VsrVersion left, VsrVersion right) => Compare(left, right) <= 0;

        public override string ToString()
        {
            return Full;
        }
    }
}
