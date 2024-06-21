﻿using Thinktecture.Internal;
namespace Thinktecture.EntityFrameworkCore.Relational.Internal
{
    public class IgnorePropertiesExpression : NonEvaluatableConstantExpression<IReadOnlyList<string>>
    {
        /// <inheritdoc />
        public IgnorePropertiesExpression(IReadOnlyList<string> value)
           : base(value)
        {
        }
        /// <inheritdoc />
        public override bool Equals(IReadOnlyList<string>? otherIgnoredProperties)
        {
            if (otherIgnoredProperties is null)
                return false;
            if (Value.Count != otherIgnoredProperties.Count)
                return false;
            foreach (var tableHint in Value)
            {
                if (!otherIgnoredProperties.Contains(tableHint))
                    return false;
            }
            return true;
        }
        /// <inheritdoc />
        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            foreach (var properties in Value)
            {
                hashCode.Add(properties);
            }
            return hashCode.ToHashCode();
        }
    }
}