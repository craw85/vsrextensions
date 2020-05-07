using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using GitCommands;
using GitUIPluginInterfaces;
using NUnit.Framework;

namespace GitCommandsTests.Git
{
    // TODO SUT is in GitUIPluginInterfaces but no test assembly exists for that assembly

    [TestFixture]
    public sealed class ObjectIdTests
    {
        [TestCase("00000000-0000-0000-0000-000000000000")]
        [TestCase("63264c77-1fde-422b-8d60-5ee40fe6b379")]
        public void TryParse_handles_valid_hashes(string sha1)
        {
            Assert.True(ObjectId.TryParse(sha1, out var id));
            Assert.AreEqual(sha1.ToLower(), id.ToString());
        }

        [TestCase("00000000-0000-0000-0000-0000000000000")]
        [TestCase("63264c77-1fde-422b-8d60-5ee40fe6b3790")]
        [TestCase("63264c77-1fde-422b-8d60-5ee40fe6b37901")]
        [TestCase("ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
        [TestCase("  00000000-0000-0000-0000-00000000000000  ")]
        public void TryParse_identifies_invalid_hashes(string sha1)
        {
            Assert.False(ObjectId.TryParse(sha1, out _));
        }

        [TestCase("00000000-0000-0000-0000-000000000000", 0)]
        [TestCase("00000000-0000-0000-0000-000000000000__", 0)]
        [TestCase("_63264c77-1fde-422b-8d60-5ee40fe6b379", 1)]
        [TestCase("_63264c77-1fde-422b-8d60-5ee40fe6b379_", 1)]
        [TestCase("__63264c77-1fde-422b-8d60-5ee40fe6b379", 2)]
        [TestCase("__63264c77-1fde-422b-8d60-5ee40fe6b379__", 2)]
        public void TryParse_with_offset_handles_valid_hashes(string sha1, int offset)
        {
            Assert.True(ObjectId.TryParse(sha1, offset, out var id));
            Assert.AreEqual(
                sha1.Substring(offset, ObjectId.GuidCharCount),
                id.ToString());
        }

        [TestCase("00000000-0000-0000-0000-000000000000")]
        [TestCase("63264c77-1fde-422b-8d60-5ee40fe6b379")]
        public void Parse_handles_valid_hashes(string sha1)
        {
            Assert.AreEqual(
                sha1.ToLower(),
                ObjectId.Parse(sha1).ToString());
        }

        [TestCase("00000000-0000-0000-0000-0000000000000")]
        [TestCase("63264c77-1fde-422b-8d60-5ee40fe6b3790")]
        [TestCase("63264c77-1fde-422b-8d60-5ee40fe6b37901")]
        [TestCase("ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
        [TestCase("  00000000-0000-0000-0000-00000000000000  ")]
        public void Parse_throws_for_invalid_hashes(string sha1)
        {
            Assert.Throws<FormatException>(() => ObjectId.Parse(sha1));
        }

        [TestCase("00000000-0000-0000-0000-000000000000")]
        [TestCase("63264c77-1fde-422b-8d60-5ee40fe6b379")]
        public void IsValid_identifies_valid_hashes(string sha1)
        {
            Assert.True(ObjectId.IsValid(sha1));
        }

        [TestCase("00000000-0000-0000-0000-0000000000000")]
        [TestCase("63264c77-1fde-422b-8d60-5ee40fe6b3790")]
        [TestCase("63264c77-1fde-422b-8d60-5ee40fe6b37901")]
        [TestCase("ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
        [TestCase("  00000000-0000-0000-0000-00000000000000  ")]
        public void IsValid_identifies_invalid_hashes(string sha1)
        {
            Assert.False(ObjectId.IsValid(sha1));
        }

        [TestCase("00000000-0000-0000-0000-000000000000", 0)]
        [TestCase("00000000-0000-0000-0000-000000000000__", 0)]
        [TestCase("_63264c77-1fde-422b-8d60-5ee40fe6b379", 1)]
        [TestCase("_63264c77-1fde-422b-8d60-5ee40fe6b379_", 1)]
        [TestCase("__63264c77-1fde-422b-8d60-5ee40fe6b379", 2)]
        [TestCase("__63264c77-1fde-422b-8d60-5ee40fe6b379__", 2)]
        public void Parse_with_offset_handles_valid_hashes(string sha1, int offset)
        {
            Assert.AreEqual(
                sha1.Substring(offset, ObjectId.GuidCharCount),
                ObjectId.Parse(sha1, offset).ToString());
        }

        [Test]
        public void ParseFromRegexCapture()
        {
            var objectId = ObjectId.Random();
            var str = "XYZ" + objectId + "XYZ";

            Assert.AreEqual(objectId, ObjectId.Parse(str, Regex.Match(str, "[a-f0-9-]{36}")));
            Assert.Throws<FormatException>(() => ObjectId.Parse(str, Regex.Match(str, "[a-f0-9-]{39}")));
            Assert.Throws<FormatException>(() => ObjectId.Parse(str, Regex.Match(str, "[XYZa-f0-9-]{39}")));
        }

        [Test]
        public void WorkTreeId_has_expected_value()
        {
            Assert.AreEqual(
                "11111111-1111-1111-1111-111111111111",
                ObjectId.WorkTreeId.ToString());
        }

        [Test]
        public void IndexId_has_expected_value()
        {
            Assert.AreEqual(
                "22222222-2222-2222-2222-222222222222",
                ObjectId.IndexId.ToString());
        }

        [Test]
        public void CombinedDiffId_has_expected_value()
        {
            Assert.AreEqual(
                "33333333-3333-3333-3333-333333333333",
                ObjectId.CombinedDiffId.ToString());
        }

        [Test]
        public void WorkTreeId_is_artificial()
        {
            Assert.IsTrue(ObjectId.WorkTreeId.IsArtificial);
        }

        [Test]
        public void IndexId_is_artificial()
        {
            Assert.IsTrue(ObjectId.IndexId.IsArtificial);
        }

        [Test]
        public void CombinedDiffId_is_artificial()
        {
            Assert.IsTrue(ObjectId.CombinedDiffId.IsArtificial);
        }

        [Test]
        public void Equivalent_ids_are_equal()
        {
            Assert.AreEqual(
                ObjectId.Parse("63264c77-1fde-422b-8d60-5ee40fe6b379"),
                ObjectId.Parse("63264c77-1fde-422b-8d60-5ee40fe6b379"));

            Assert.AreEqual(
                ObjectId.WorkTreeId,
                ObjectId.WorkTreeId);

            Assert.AreEqual(
                ObjectId.WorkTreeId,
                ObjectId.Parse(GitRevision.WorkTreeGuid));

            Assert.AreEqual(
                ObjectId.IndexId,
                ObjectId.IndexId);

            Assert.AreEqual(
                ObjectId.IndexId,
                ObjectId.Parse(GitRevision.IndexGuid));

            Assert.AreEqual(
                ObjectId.CombinedDiffId,
                ObjectId.CombinedDiffId);

            Assert.AreEqual(
                ObjectId.CombinedDiffId,
                ObjectId.Parse(GitRevision.CombinedDiffGuid));
        }

        [Test]
        public void Different_ids_are_not_equal()
        {
            Assert.AreNotEqual(
                ObjectId.Parse("00000000-0000-0000-0000-000000000000"),
                ObjectId.Parse("63264c77-1fde-422b-8d60-5ee40fe6b379"));

            Assert.AreNotEqual(
                ObjectId.WorkTreeId,
                ObjectId.IndexId);
        }

        [Test]
        public void Equivalent_ids_have_equal_hash_codes()
        {
            Assert.AreEqual(
                ObjectId.Parse("63264c77-1fde-422b-8d60-5ee40fe6b379").GetHashCode(),
                ObjectId.Parse("63264c77-1fde-422b-8d60-5ee40fe6b379").GetHashCode());

            Assert.AreEqual(
                ObjectId.WorkTreeId.GetHashCode(),
                ObjectId.WorkTreeId.GetHashCode());

            Assert.AreEqual(
                ObjectId.IndexId.GetHashCode(),
                ObjectId.IndexId.GetHashCode());
        }

        [Test]
        public void Different_ids_have_different_hash_codes()
        {
            Assert.AreNotEqual(
                ObjectId.Parse("00000000-0000-0000-0000-000000000000").GetHashCode(),
                ObjectId.Parse("63264c77-1fde-422b-8d60-5ee40fe6b379").GetHashCode());

            Assert.AreNotEqual(
                ObjectId.WorkTreeId.GetHashCode(),
                ObjectId.IndexId.GetHashCode());
        }

        [Test]
        [SuppressMessage("ReSharper", "ReturnValueOfPureMethodIsNotUsed")]
        public void ToShortString()
        {
            const string s = "63264c77-1fde-422b-8d60-5ee40fe6b379";
            var id = ObjectId.Parse(s);

            for (var length = 0; length < ObjectId.GuidCharCount; length++)
            {
                Assert.AreEqual(s.Substring(0, length), id.ToShortString(length));
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => id.ToShortString(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => id.ToShortString(ObjectId.GuidCharCount + 1));
        }

        [Test]
        public void Equals_with_string()
        {
            for (var i = 0; i < 100; i++)
            {
                var objectId = ObjectId.Random();
                Assert.True(objectId.Equals(objectId.ToString()));
            }

            Assert.False(ObjectId.Random().Equals((string)null));
            Assert.False(ObjectId.Random().Equals(""));
            Assert.False(ObjectId.Random().Equals("gibberish"));
            Assert.False(ObjectId.Parse("63264c77-1fde-422b-8d60-5ee40fe6b379").Equals(" 63264c77-1fde-422b-8d60-5ee40fe6b379 "));
            Assert.False(ObjectId.Parse("63264c77-1fde-422b-8d60-5ee40fe6b379").Equals("63264C77-1FDE-422B-8D60-5EE40FE6B379"));
        }

        [Test]
        public void Equals_using_operator()
        {
            string objectIdString = "63264c77-1fde-422b-8d60-5ee40fe6b379";
            Assert.IsTrue(ObjectId.Parse(objectIdString) == ObjectId.Parse(objectIdString));
            Assert.IsFalse(ObjectId.Parse(objectIdString) != ObjectId.Parse(objectIdString));
            Assert.IsFalse(ObjectId.Parse(objectIdString) == ObjectId.Random());
            Assert.IsTrue(ObjectId.Parse(objectIdString) != ObjectId.Random());
        }
    }
}
