using System;

namespace Silky.Validation.StringValues
{
    [Serializable]
    [ValueValidator("NULL")]
    public class AlwaysValidValueValidator : ValueValidatorBase
    {
        public override bool IsValid(object value)
        {
            return true;
        }
    }
}