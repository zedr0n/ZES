using System;

namespace ZES.Infrastructure
{
    public class FluentBuilder
    {
        protected static T Clone<T>(T builder, Action<T> setter)
            where T : FluentBuilder
        {
            var b = (T)builder.MemberwiseClone();
            setter(b);
            return b;
        } 
    }
}