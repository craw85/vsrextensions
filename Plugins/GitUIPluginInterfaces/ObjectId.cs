using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using JetBrains.Annotations;

namespace GitUIPluginInterfaces
{
    /// <summary>
    /// Models a SHA1 hash.
    /// </summary>
    /// <remarks>
    /// <para>Instances are immutable and are guaranteed to contain valid, 160-bit (20-byte) SHA1 hashes.</para>
    /// <para>String forms of this object must be in lower case.</para>
    /// </remarks>
    public sealed class ObjectId : IEquatable<ObjectId>, IComparable<ObjectId>
    {
        /// <summary>
        /// Gets the artificial ObjectId used to represent working directory tree (unstaged) changes.
        /// </summary>
        [NotNull]
        public static ObjectId WorkTreeId { get; } = new ObjectId(new Guid("11111111-1111-1111-1111-111111111111"));

        /// <summary>
        /// Gets the artificial ObjectId used to represent changes staged to the index.
        /// </summary>
        [NotNull]
        public static ObjectId IndexId { get; } = new ObjectId(new Guid("22222222-2222-2222-2222-222222222222"));

        /// <summary>
        /// Gets the artificial ObjectId used to represent combined diff for merge commits.
        /// </summary>
        [NotNull]
        public static ObjectId CombinedDiffId { get; } = new ObjectId(new Guid("33333333-3333-3333-3333-333333333333"));

        /// <summary>
        /// Produces an <see cref="ObjectId"/> populated with random bytes.
        /// </summary>
        [NotNull]
        [MustUseReturnValue]
        public static ObjectId Random()
        {
            return new ObjectId(Guid.NewGuid());
        }

        public bool IsArtificial => this == WorkTreeId || this == IndexId || this == CombinedDiffId;

        public const int GuidCharCount = 36;

        #region Parsing

        /// <summary>
        /// Attempts to parse an <see cref="ObjectId"/> from <paramref name="s"/>.
        /// </summary>
        /// <remarks>
        /// For parsing to succeed, <paramref name="s"/> must be a valid 40-character SHA-1 string.
        /// Any extra characters at the end will cause parsing to fail, unlike for
        /// overload <see cref="TryParse(string,int,out ObjectId)"/>.
        /// </remarks>
        /// <param name="s">The string to try parsing from.</param>
        /// <param name="objectId">The parsed <see cref="ObjectId"/>, or <c>null</c> if parsing was unsuccessful.</param>
        /// <returns><c>true</c> if parsing was successful, otherwise <c>false</c>.</returns>
        [ContractAnnotation("=>false,objectId:null")]
        [ContractAnnotation("=>true,objectId:notnull")]
        public static bool TryParse([CanBeNull] string s, out ObjectId objectId)
        {
            if (s == null || s.Length != GuidCharCount)
            {
                objectId = default;
                return false;
            }

            return TryParse(s, 0, out objectId);
        }

        /// <summary>
        /// Attempts to parse an <see cref="ObjectId"/> from <paramref name="s"/>, starting at <paramref name="offset"/>.
        /// </summary>
        /// <remarks>
        /// For parsing to succeed, <paramref name="s"/> must contain a valid 40-character SHA-1 starting at <paramref name="offset"/>.
        /// Any extra characters before or after this substring will be ignored, unlike for
        /// overload <see cref="TryParse(string,out ObjectId)"/>.
        /// </remarks>
        /// <param name="s">The string to try parsing from.</param>
        /// <param name="offset">The position within <paramref name="s"/> to start parsing from.</param>
        /// <param name="objectId">The parsed <see cref="ObjectId"/>, or <c>null</c> if parsing was unsuccessful.</param>
        /// <returns><c>true</c> if parsing was successful, otherwise <c>false</c>.</returns>
        [ContractAnnotation("=>false,objectId:null")]
        [ContractAnnotation("=>true,objectId:notnull")]
        public static bool TryParse([CanBeNull] string s, int offset, out ObjectId objectId)
        {
            if (s == null || s.Length - offset < GuidCharCount)
            {
                objectId = default;
                return false;
            }

            string strId = s.Substring(offset, GuidCharCount);

            if (Guid.TryParse(strId, out Guid id))
            {
                objectId = new ObjectId(id);
                return true;
            }

            objectId = default;
            return false;
        }

        /// <summary>
        /// Parses an <see cref="ObjectId"/> from <paramref name="s"/>.
        /// </summary>
        /// <remarks>
        /// For parsing to succeed, <paramref name="s"/> must be a valid 40-character SHA-1 string.
        /// Any extra characters at the end will cause parsing to fail, unlike for
        /// overload <see cref="Parse(string,int)"/>.
        /// </remarks>
        /// <param name="s">The string to try parsing from.</param>
        /// <returns>The parsed <see cref="ObjectId"/>.</returns>
        /// <exception cref="FormatException"><paramref name="s"/> did not contain a valid 40-character SHA-1 hash.</exception>
        [NotNull]
        [MustUseReturnValue]
        public static ObjectId Parse([NotNull] string s)
        {
            if (s == null || s.Length != GuidCharCount || !TryParse(s, 0, out var id))
            {
                throw new FormatException($"Unable to parse object ID \"{s}\".");
            }

            return id;
        }

        /// <summary>
        /// Parses an <see cref="ObjectId"/> from <paramref name="s"/>.
        /// </summary>
        /// <remarks>
        /// For parsing to succeed, <paramref name="s"/> must contain a valid 40-character SHA-1 starting at <paramref name="offset"/>.
        /// Any extra characters before or after this substring will be ignored, unlike for
        /// overload <see cref="Parse(string)"/>.
        /// </remarks>
        /// <param name="s">The string to try parsing from.</param>
        /// <param name="offset">The position within <paramref name="s"/> to start parsing from.</param>
        /// <returns>The parsed <see cref="ObjectId"/>.</returns>
        /// <exception cref="FormatException"><paramref name="s"/> did not contain a valid 40-character SHA-1 hash.</exception>
        [NotNull]
        [MustUseReturnValue]
        public static ObjectId Parse([NotNull] string s, int offset)
        {
            if (!TryParse(s, offset, out var id))
            {
                throw new FormatException($"Unable to parse object ID \"{s}\" at offset {offset}.");
            }

            return id;
        }

        /// <summary>
        /// Parses an <see cref="ObjectId"/> from a regex <see cref="Capture"/> that was produced by matching against <paramref name="s"/>.
        /// </summary>
        /// <remarks>
        /// <para>This method avoids the temporary string created by calling <see cref="Capture.Value"/>.</para>
        /// <para>For parsing to succeed, <paramref name="s"/> must be a valid 40-character SHA-1 string.</para>
        /// </remarks>
        /// <param name="s">The string that the regex <see cref="Capture"/> was produced from.</param>
        /// <param name="capture">The regex capture/group that describes the location of the SHA-1 hash within <paramref name="s"/>.</param>
        /// <returns>The parsed <see cref="ObjectId"/>.</returns>
        /// <exception cref="FormatException"><paramref name="s"/> did not contain a valid 40-character SHA-1 hash.</exception>
        [NotNull]
        [MustUseReturnValue]
        public static ObjectId Parse([NotNull] string s, [NotNull] Capture capture)
        {
            if (s == null || capture == null || capture.Length != GuidCharCount || !TryParse(s, capture.Index, out var id))
            {
                throw new FormatException($"Unable to parse object ID \"{s}\".");
            }

            return id;
        }

        #endregion

        /// <summary>
        /// Identifies whether <paramref name="s"/> contains a valid 40-character SHA-1 hash.
        /// </summary>
        /// <param name="s">The string to validate.</param>
        /// <returns><c>true</c> if <paramref name="s"/> is a valid SHA-1 hash, otherwise <c>false</c>.</returns>
        [Pure]
        public static bool IsValid([NotNull] string s) => s.Length == GuidCharCount && IsValidCharacters(s);

        /// <summary>
        /// Identifies whether <paramref name="s"/> contains between <paramref name="minLength"/> and 40 valid SHA-1 hash characters.
        /// </summary>
        /// <param name="s">The string to validate.</param>
        /// <returns><c>true</c> if <paramref name="s"/> is a valid partial SHA-1 hash, otherwise <c>false</c>.</returns>
        [Pure]
        public static bool IsValidPartial([NotNull] string s, int minLength) => s.Length >= minLength && s.Length <= GuidCharCount && IsValidCharacters(s);

        private static bool IsValidCharacters(string s)
        {
            // ReSharper disable once LoopCanBeConvertedToQuery
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (!char.IsDigit(c) && (c < 'a' || c > 'f') && c != '-')
                {
                    return false;
                }
            }

            return true;
        }

        private readonly Guid _id;

        public ObjectId(Guid id)
        {
            _id = id;
        }

        #region IComparable<ObjectId>

        public int CompareTo(ObjectId other)
        {
            var result = 0;

            _ = Compare(_id, other._id);

            return result;

            bool Compare(Guid i, Guid j)
            {
                var c = i.CompareTo(j);

                if (c != 0)
                {
                    result = c;
                    return true;
                }

                return false;
            }
        }

        #endregion

        /// <summary>
        /// Returns the Guid.
        /// </summary>
        public override string ToString()
        {
            return ToShortString(GuidCharCount);
        }

        /// <summary>
        /// Returns the first <paramref name="length"/> characters of the Guid hash.
        /// </summary>
        /// <param name="length">The length of the returned string. Defaults to <c>10</c>.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero, or more than 36.</exception>
        [Pure]
        [NotNull]
        public unsafe string ToShortString(int length = 8)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), length, "Cannot be less than zero.");
            }

            if (length > GuidCharCount)
            {
                throw new ArgumentOutOfRangeException(nameof(length), length, $"Cannot be greater than {GuidCharCount}.");
            }

            return _id.ToString().Substring(0, length);
        }

        #region Equality and hashing

        /// <inheritdoc />
        public bool Equals(ObjectId other)
        {
            return other != null &&
                   _id == other._id;
        }

        /// <summary>
        /// Gets whether <paramref name="other"/> is equivalent to this <see cref="ObjectId"/>.
        /// </summary>
        /// <remarks>
        /// <para>This method does not allocate.</para>
        /// <para><paramref name="other"/> must be lower case and not have any surrounding white space.</para>
        /// </remarks>
        public bool Equals([CanBeNull] string other)
        {
            if (other == null || other.Length != GuidCharCount)
            {
                return false;
            }

            return other.Equals(_id.ToString());
        }

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is ObjectId id && Equals(id);

        /// <inheritdoc />
        public override int GetHashCode() => _id.GetHashCode();

        public static bool operator ==(ObjectId left, ObjectId right) => Equals(left, right);
        public static bool operator !=(ObjectId left, ObjectId right) => !Equals(left, right);

        #endregion
    }
}
