// 일부 게임 버전의 mscorlib 에서는 DefaultInterpolatedStringHandler 가 공개되어 있지 않아
// $"..." 문자열이 MethodAccessException 을 냄 → 모드 안에 같은 이름의 핸들러를 직접 둠.
using System.Text;

namespace System.Runtime.CompilerServices
{
    internal struct DefaultInterpolatedStringHandler
    {
        StringBuilder _sb;
        public DefaultInterpolatedStringHandler(int literalLength, int formattedCount)
        { _sb = new StringBuilder(literalLength + formattedCount * 8); }
        public DefaultInterpolatedStringHandler(int literalLength, int formattedCount, IFormatProvider provider)
            : this(literalLength, formattedCount) { }

        public void AppendLiteral(string value) { _sb.Append(value); }
        public void AppendFormatted(string value) { _sb.Append(value); }
        public void AppendFormatted(string value, int alignment = 0, string format = null) { Pad(value, alignment); }
        public void AppendFormatted(object value, int alignment = 0, string format = null) { Pad(Fmt(value, format), alignment); }
        public void AppendFormatted<T>(T value) { _sb.Append(Fmt(value, null)); }
        public void AppendFormatted<T>(T value, string format) { _sb.Append(Fmt(value, format)); }
        public void AppendFormatted<T>(T value, int alignment) { Pad(Fmt(value, null), alignment); }
        public void AppendFormatted<T>(T value, int alignment, string format) { Pad(Fmt(value, format), alignment); }

        static string Fmt(object v, string format)
        {
            if (v == null) return "";
            if (format != null && v is IFormattable f) return f.ToString(format, null);
            return v.ToString();
        }
        void Pad(string s, int alignment)
        {
            s = s ?? "";
            if (alignment > 0) _sb.Append(s.PadLeft(alignment));
            else if (alignment < 0) _sb.Append(s.PadRight(-alignment));
            else _sb.Append(s);
        }
        public override string ToString() => _sb.ToString();
        public string ToStringAndClear() { var s = _sb.ToString(); _sb.Clear(); return s; }
    }
}
