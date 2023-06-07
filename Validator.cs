using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Ps = Limcap.LightValidator.Subject<string>;

namespace Limcap.LightValidator {

	/// <summary>
	///	Provides validation for any object and its members.
	/// </summary>
	public class Validator {

		public Validator(string group = null) { CurrentGroup = group; }

		internal string _group;
		internal string _subject;
		internal dynamic _value;
		internal bool _passed;
		internal readonly List<string> _errors = new List<string>();
		internal readonly Report _report = new Report();

		public string CurrentGroup { get => _group; set => _group = value; }
		public string CurrentSubject { get => _subject; set => _subject = value; }
		public List<string> SubjectErrors { get => _errors; }
		public Report CurrentReport => _report;
		public bool SubjectIsValid { get => _errors.Any(); }

		public void Reset() { ResetSubject(); _report.Reset(); }
		public void ResetSubject() { _errors.Clear(); _subject = null; _value = null; _passed = false; }
		public ValidationGroup NewGroup(string name) => new ValidationGroup(name, this);
		public Subject<dynamic> NewSubject(string name) => Ext_Validator.NewSubject<dynamic>(this, name, null);
		public Subject<string> NewSubject(string name, string value) => Ext_Validator.NewSubject(this, name, value);
		public Subject<IEnumerable<V>> NewSubject<V>(string name, IEnumerable<V> value) => Ext_Validator.NewSubject(this, name, value);

		internal void AddLog(string msg) {
			if (_errors.Any()) return;
			_errors.Add(msg);
			var title = _group != null ? $"{_group}, {_subject}" : _subject;
			_report.Add(title, msg);
		}
	}






	public static class Ext_Validator {
		// Esse nétodo precisa ser de extensão senão ele tem precedência na resolução de overloading
		// do linter sobre o NewSubject<IEnumerable<V>>, o que faz com que as chamadas do método NewSubject com
		// um value que seja IEnumerable seja identificado incorretamente pelo linter, e então as chamadas
		// para os métodos de extensão de NewSubject<IEnumerable<V>> ficam marcados como erro no linter.
		// Sendo extensão, ele cai na hierarquia de resolução resolvendo o problema.
		public static Subject<V> NewSubject<V>(this Validator v, string name, V value) {
			v._subject = name;
			v._value = value;
			v._passed = false;
			v._errors.Clear();
			return new Subject<V>(v);
		}
	}






	public struct ValidationGroup {
		public ValidationGroup(string group, Validator v) {
			v._group = group;
			V = v;
		}
		internal Validator V;

		public string GroupName { get => V._group; }
		public string SubjectName { get => V._subject; set => V._subject = value; }
		public List<string> SubjectErrors => V._errors;
		public bool SubjectIsValid => V.SubjectIsValid;

		public Subject<dynamic> NewSubject(string name) => V.NewSubject(name);
		public Subject<string> NewSubject(string name, string value) => V.NewSubject(name, value);
		public Subject<IEnumerable<T>> NewSubject<T>(string name, IEnumerable<T> value) => V.NewSubject(name, value);
		public Subject<T> NewSubject<T>(string name, T value) => V.NewSubject(name, value);
	}






	public class Report {
		public List<Log> Logs { get; private set; } = new List<Log>();
		public bool HasErrors { get => Logs?.Any() ?? false; }
		public void Reset() { Logs.Clear(); }
		public void Include(Report report) { Include(null, report); }
		public void Include(string group, Report report) { foreach (var log in report.Logs) Add(group, log); }
		public void Add(Log log) { Add(null, log); }
		public void Add(string title, string text) { Logs.Add(new Log(title, text)); }
		public void Add(string title, Log log) {
			var subject = title != null ? $"{title}, {log.Subject}" : log.Subject;
			if (log.Description != null && log.Description.Any()) Logs.Add(new Log(subject, log.Description));
		}
	}






	public struct Subject<V> {

		internal Subject(Validator v) { this.v = v; }
		private Validator v;

		public string Name { get => v._subject; private set => v._subject = value; }
		public V Value { get => IsRightValueType ? v._value : default(V); internal set => v._value = value; }
		public bool IsValid => v.SubjectIsValid;
		public bool PreviousTestHasPassed { get => v._passed; }
		private bool IsRightValueType => v._value == null && default(V) == null || v._value.GetType() == typeof(V);
		internal List<string> Errors => v._errors;

		public Subject<V> SetValue(V value) { Value = value; return this; }

		public Subject<T> Cast<T>() {
			var newSubject = new Subject<T>(v);
			if (!v.SubjectIsValid) return newSubject;
			try {	v._value = (T)v._value;	}
			catch { v._value = default(T); }
			return newSubject;
		}

		public Subject<T> To<T>(Func<V, T> converter, string msg = null) {
			var newSubject = new Subject<T>(v);
			if (!v.SubjectIsValid) return newSubject;
			try { v._value = converter(v._value); }
			catch (Exception ex) {
				var exInfo = $"[{ex.GetType().Name}: {ex.Message}]";
				v.AddLog(msg ?? DefaultConvertMsg<T>(exInfo));
				v._value = default(T);
			}
			return newSubject;
		}

		public Subject<T> To<T, S>(Func<V, S, T> converter, S supplement, string msg = null) {
			var newSubject = new Subject<T>(v);
			if (!v.SubjectIsValid) return newSubject;
			try { v._value = converter(v._value, supplement); }
			catch (Exception ex) {
				var exInfo = $"{ex.GetType().Name}: {ex.Message}";
				//v.AddErrorMessage(msg ?? DefaultConvertMsg<T>(exInfo));
				v.AddLog(msg ?? DefaultConvertMsg<T>(exInfo));
				v._value = default(S);
			}
			return newSubject;
		}

		static string DefaultConvertMsg<T>(string info) => $"Não é um valor válido para o tipo '{typeof(T).Name}' - [{info}]";

		public Subject<V> Check(string failureMessage, ValidationTest<V> test) {
			if (!v.SubjectIsValid) return this;
			try {
				var success = test(v._value);
				if (!success) v.AddLog(failureMessage);
				v._passed = success;
			}
			catch (Exception ex) {
				v.AddLog("[Exception] " + ex.Message);
				v._passed = false;
			}
			return this;
		}

		public Subject<V> Check<A>(string failureMessage, ValidationTest<V, A> test, A testArg) {
			if (!v.SubjectIsValid) return this;
			try {
				var success = test(v._value, testArg);
				if (!success) v.AddLog(failureMessage);
				v._passed = success;
			}
			catch (Exception ex) {
				v.AddLog("[Exception] " + ex.Message);
				v._passed = false;
			}
			return this;
		}

		public Subject<V> Check(string failureMessage, bool test) {
			if (!v.SubjectIsValid) return this;
			v._passed = test;
			if (!test) v.AddLog(failureMessage);
			return this;
		}

		public Subject<V> Check(bool test) => Check("Valor inválido", test);
	}






	[DebuggerDisplay("{DD(), nq}")]
	public struct Log {
		public Log(string subject, string description) { Subject = subject; Description = description; }

		public readonly string Subject;
		public readonly string Description;
		#if DEBUG
		private string DD() {
			var str1 = Subject is null ? "[No Subject]" : $"\"{Subject}\"";
			var str2 = Description is null ? $"[No Description]" : $"\"{Description}\"";
			return $"{str1} ==> {str2}";
		}
		#endif
	}






	public delegate bool ValidationTest<V>(V value);
	public delegate bool ValidationTest<V, R>(V value, R allowed = default);
	public delegate void ValidationScript(Validator v);






	internal static class Tests {
		public static bool IsNull<V>(V x) => x == null;
		public static bool IsNotNull<V>(V x) => x != null;
		public static bool IsEmpty<V>(IEnumerable<V> x) => x == null || x.Count() > 0;
		public static bool IsNotEmpty<V>(IEnumerable<V> x) => x != null && x.Count() > 0;
		public static bool IsBlank(string x) => string.IsNullOrWhiteSpace(x);
		public static bool IsNotBlank(string x) => !string.IsNullOrWhiteSpace(x);
		public static bool IsMatch(string x, string a) => x != null && Regex.IsMatch(x, a);
		public static bool IsIn<V>(V x, IEnumerable<V> a) => x != null && a.Contains(x);
		public static bool IsEqual<V>(V x, V a) where V : IEquatable<V> => x != null && x.Equals(a);
		public static bool IsMinLength<V>(IEnumerable<V> x, int t) => x != null && x.Count() >= t;
		public static bool IsMaxLength<V>(IEnumerable<V> x, int t) => x == null || x.Count() <= t;
		public static bool IsLength<V>(IEnumerable<V> x, int t) => x != null && x.Count() == t;
		public static bool IsAtLeast<V>(V x, V t) where V : IComparable<V> => x != null && x.CompareTo(t) >= 0;
		public static bool IsAtMost<V>(V x, V t) where V : IComparable<V> => x == null || x.CompareTo(t) <= 0;
		public static bool IsExactly<V>(V x, V t) where V : IComparable<V> => x != null && x.CompareTo(t) == 0;
		public static bool IsEmail(string x) => Regex.IsMatch(x, @"^\w+([.-]?\w+)*@\w+([.-]?\w+)*(\.\w{2,3})+$");
		public static bool IsDigitsOnly(string x) => x != null && x.All(y => char.IsDigit(y));
	}






	public static class Ext_Subject_Checks {
		// generic
		public static Subject<V> IsNull<V>(this Subject<V> p, string msg = null) {
			p.Check(msg ?? $"Deve ser nulo", Tests.IsNull); return p;
		}
		public static Subject<V> IsNotNull<V>(this Subject<V> p, string msg = null) {
			p.Check(msg ?? $"Não pode ser nulo", Tests.IsNotNull); return p;
		}
		public static Subject<V> IsIn<V>(this Subject<V> p, IEnumerable<V> options, string msg = null) {
			p.Check(msg ?? $"Não é um valor válido", Tests.IsIn, options); return p;
		}
		public static Subject<V> IsIn<V>(this Subject<V> p, params V[] options) {
			p.Check($"Não é um opção válida", Tests.IsIn, options); return p;
		}

		// IEquatable
		public static Subject<V> IsEquals<V>(this Subject<V> p, V value, string msg = null) where V : IEquatable<V> {
			p.Check(msg ?? $"Deve ser {value}", Tests.IsEqual, value); return p;
		}

		// IComparable
		public static Subject<V> IsAtLeast<V>(this Subject<V> p, V minValue, string msg = null) where V : IComparable<V> {
			p.Check(msg ?? $"Não pode ser menor que {minValue}", Tests.IsAtLeast, minValue); return p;
		}
		public static Subject<V> IsAtMost<V>(this Subject<V> p, V maxValue, string msg = null) where V : IComparable<V> {
			p.Check(msg ?? $"Não pode ser maior que {maxValue}", Tests.IsAtMost, maxValue); return p;
		}
		public static Subject<V> Is<V>(this Subject<V> p, V value, string msg = null) where V : IComparable<V> {
			p.Check(msg ?? $"Deve ser exatamente {value}", Tests.IsExactly, value); return p;
		}

		// IEnumerable
		public static Subject<IEnumerable<V>> IsEmpty<V>(this Subject<IEnumerable<V>> p, string msg = null) {
			p.Check(msg ?? $"Deve ficar vazio", Tests.IsEmpty); return p;
		}
		public static Subject<IEnumerable<V>> IsNotEmpty<V>(this Subject<IEnumerable<V>> p, string msg = null) {
			p.Check(msg ?? $"Não pode ficar vazio", Tests.IsNotEmpty); return p;
		}
		public static Subject<IEnumerable<V>> HasLength<V>(this Subject<IEnumerable<V>> p, int length, string msg = null) {
			p.Check(msg ?? $"Deve ter exatamente {length} itens", Tests.IsLength, length); return p;
		}
		public static Subject<IEnumerable<V>> HasMinLength<V>(this Subject<IEnumerable<V>> p, int length, string msg = null) {
			p.Check(msg ?? $"Não pode ser menor que {length} itens", Tests.IsMinLength, length); return p;
		}
		public static Subject<IEnumerable<V>> HasMaxLength<V>(this Subject<IEnumerable<V>> p, int length, string msg = null) {
			p.Check(msg ?? $"Não pode ser maior que {length} itens", Tests.IsMaxLength, length); return p;
		}

		// int
		public static Subject<int> IsAtLeast(this Subject<int> p, int number, string msg = null) {
			p.Check(msg ?? $"Não pode ser menor que {number}", Tests.IsAtLeast, number); return p;
		}
		public static Subject<int> IsAtMost(this Subject<int> p, int number, string msg = null) {
			p.Check(msg ?? $"Não pode ser maior que {number}", Tests.IsAtMost, number); return p;
		}
		public static Subject<int> Is(this Subject<int> p, int number, string msg = null) {
			p.Check(msg ?? $"Deve ser exatamente {number}", Tests.IsExactly, number); return p;
		}

		// string
		public static Subject<string> IsEmpty(this Subject<string> p, string msg = null) {
			p.Check(msg ?? $"Não deve ser preenchido", Tests.IsEmpty); return p;
		}
		public static Subject<string> IsNotEmpty(this Subject<string> p, string msg = null) {
			p.Check(msg ?? $"Não está preenchido", Tests.IsNotEmpty); return p;
		}
		public static Subject<string> IsBlank(this Subject<string> p, string msg = null) {
			p.Check(msg ?? $"Deve ficar em branco", Tests.IsBlank); return p;
		}
		public static Subject<string> IsNotBlank(this Subject<string> p, string msg = null) {
			p.Check(msg ?? $"Não está preenchido", Tests.IsNotEmpty); return p;
		}
		public static Subject<string> HasLength(this Subject<string> p, int length, string msg = null) {
			p.Check(msg ?? $"Deve ter exatamente {length} caracteres", Tests.IsLength, length); return p;
		}
		public static Subject<string> HasMinLength(this Subject<string> p, int length, string msg = null) {
			p.Check(msg ?? $"Não pode ser menor que {length} caracteres", Tests.IsMinLength, length); return p;
		}
		public static Subject<string> HasMaxLength(this Subject<string> p, int length, string msg = null) {
			p.Check(msg ?? $"Não pode ser maior que {length} caracteres", Tests.IsMaxLength, length); return p;
		}
		public static Subject<string> IsMatch(this Subject<string> p, string pattern, string msg = null) {
			p.Check(msg ?? "Não é um valor aceito", Tests.IsMatch, pattern); return p;
		}
		public static Subject<string> IsEmail(this Subject<string> p, string msg = null) {
			p.Check(msg ?? "Não é um e-mail válido", Tests.IsEmail); return p;
		}
		public static Subject<string> IsDigitsOnly(this Subject<string> p, string msg = null) {
			p.Check(msg ?? "Deve conter somente digitos (0-9)", Tests.IsDigitsOnly); return p;
		}
	}



	public static class Ext_Subject_Numeric {
		public static Ps AsString<T>(this Subject<T> p) => p.To(o => o.ToString());
		public static Subject<byte> AsByte(this Ps p) => _to(p, o => byte.Parse(o));
		public static Subject<short> AsShort(this Ps p) => _to(p, o => short.Parse(o));
		public static Subject<ushort> AsUshort(this Ps p) => _to(p, o => ushort.Parse(o));
		public static Subject<int> AsInt(this Ps p) => _to(p, o => int.Parse(o));
		public static Subject<uint> AsUint(this Ps p) => _to(p, o => uint.Parse(o));
		public static Subject<long> AsLong(this Ps p) => _to(p, o => long.Parse(o));
		public static Subject<ulong> AsUlong(this Ps p) => _to(p, o => ulong.Parse(o));
		public static Subject<float> AsFloat(this Ps p) => _to(p, o => float.Parse(o));
		public static Subject<decimal> AsDecimal(this Ps p) => _to(p, o => decimal.Parse(o));
		public static Subject<double> AsDouble(this Ps p) => _to(p, o => double.Parse(o));
		public static Subject<DateTime> AsDateTime(this Ps p, string format) => p.To(_parseDT, format);
		public static Subject<DateTime> AsDate(this Ps p) => p.To(o => o.ToDate(), "Não é uma data válida");
		public static Subject<DateTime?> AsNullableDate(this Ps p) => p.To(o => o.ToNullableDate(), "Não é uma data válida");

		static Subject<T> _to<T>(Ps p, Func<string, T> converter) => p.To(converter, _msgN<T>());
		static string _msgN<N>() => $"Não é um número válido do tipo '{typeof(N).Name}'";
		static DateTime _parseDT(string v, string f) => DateTime.ParseExact(v, f, CultureInfo.CurrentCulture);
	}






	public static class Ext_Subject {
		public static Subject<T> GetCurrentValue<T>(this Subject<T> p, out T variable) { variable = p.Value; return p; }
	}






	public static class Ext_Subject_String {
		public static Ps Trim(this Ps p, char c = (char)0) { p.Value = c == 0 ? p.Value.Trim() : p.Value.Trim(c); return p; }
		public static Ps ToLower(this Ps p) { p.Value = p.Value?.ToLower(); return p; }
		public static Ps ToUpper(this Ps p) { p.Value = p.Value?.ToUpper(); return p; }
		public static Ps RemoveDiacritics(this Ps p) { p.Value = p.Value?.RemoveDiacritics(); return p; }
		public static Ps ToASCII(this Ps p) { p.Value = p.Value?.ToASCII(); return p; }
		public static Ps Crop(this Ps p, int startIndex, int length) { p.Value = p.Value?.Crop(startIndex, length); return p; }
		public static Ps Replace(this Ps p, string oldStr, string newStr) { p.Value = p.Value?.Replace(oldStr, newStr); return p; }
		public static Ps Replace(this Ps p, char oldChar, char newChar) { p.Value = p.Value?.Replace(oldChar, newChar); return p; }
		public static Ps Replace(this Ps p, params ValueTuple<char, char>[] pairs) { p.Value = p.Value.Replace(pairs); return p; }
		public static Ps RemoveChars(this Ps p, string chars) { p.Value = p.Value.RemoveChars(chars); return p; }
		public static Ps RemoveChars(this Ps p, IEnumerable<char> chars) { p.Value = p.Value.RemoveChars(chars); return p; }
		public static Ps RemoveChars(this Ps p, params char[] chars) { p.Value = p.Value.RemoveChars(chars); return p; }
	}






	public static class Ext_String {

		public static string RemoveChars(this string str, string chars) {
			return Regex.Replace(str, $"[{Regex.Escape(chars)}]", "");
		}

		public static string RemoveChars(this string str, IEnumerable<char> chars) {
			return RemoveChars(str, string.Join(string.Empty, chars));
		}

		public static string RemoveChars(this string str, params char[] chars) {
			return RemoveChars(str, string.Join(string.Empty, chars));
		}

		public static string Crop(this string str, int startIndex, int length) {
			return startIndex + length > str.Length - 1 ? str.Substring(startIndex) : str.Substring(startIndex, length);
		}

		public static string Replace(this string str, params ValueTuple<char, char>[] pairs) {
			var array = str.ToCharArray();
			for (int i = 0; i < array.Length; i++) {
				var pair = findPair(array[i]);
				if (pair != null) array[i] = pair.Value.Item2;
			}
			(char, char)? findPair(char key) {
				for (int j = 0; j < pairs.Length; j++) if (pairs[j].Item1 == key) return pairs[j];
				return null;
			}
			return array.ToString();
		}

		public static string RemoveDiacritics(this string text) {
			var normalizedString = text.Normalize(NormalizationForm.FormD);
			var sb = new StringBuilder(normalizedString.Length);
			for (int i = 0; i < normalizedString.Length; i++) {
				char c = normalizedString[i];
				var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
				if (unicodeCategory != UnicodeCategory.NonSpacingMark) sb.Append(c);
			}
			return sb.ToString().Normalize(NormalizationForm.FormC);
		}

		public static string ToASCII(this string text, string replaceInvalidWith = "~") {
			return Regex.Replace(RemoveDiacritics(text), @"[^\u0000-\u007F]", replaceInvalidWith);
		}

		public static string RemoveNonASCII(this string text) {
			return Regex.Replace(RemoveDiacritics(text), @"[^\u0000-\u007F]+", string.Empty);
		}

		public static bool IsEmpty(this string str) { return string.IsNullOrEmpty(str); }
		public static bool IsBlank(this string str) { return string.IsNullOrWhiteSpace(str); }

		public static DateTime? ToNullableDate(this string str) { try { return str.ToDate(); } catch { return null; } }
		public static DateTime ToDate(this string str) {
			int start =-1, len = 0;
			int p1 = 0, p2 = 0, p3 = 0;
			char sep = '\0';
			for (int i=0;i<str.Length+1;i++){
				char c = i == str.Length ? '\0' : str[i];
				if (start == -1) {
					if (!char.IsDigit(c)) throw new ArgumentException();
					start = i; len++;
				}
				else if (char.IsDigit(c)) {
					if (len == 4) throw new ArgumentException();
					len++;
				}
				else {
					if (sep == '\0') sep = c;
					else if (c != sep && p3 != 0) throw new ArgumentException();
					int px = int.Parse(str.Substring(start,len));
					if (p1 == 0) p1 = px;
					else if( p2 == 0) p2 = px;
					else if (p3 == 0) {
						if (c == sep) throw new ArgumentException();
						p3 = px;
						break;
					}
					start = -1; len = 0;
				}
			}
			int year, month, day, m1, m2;
			if (p1 > 31) { year = p1; month = p2; day = p3; }
			else {
				year = p3;
				if( p2 < 13) { month = p2; day=p1; }
				else { month = p1; day = p2; }
			}
			if (month > 12 || day > 31) throw new ArgumentException();
			return new DateTime(year, month, day);
		}

		//public static IEnumerable<string> Trim(this IEnumerable<string> texts) => texts.Select(u => u.Trim());
		//public static IEnumerable<string> ToLower(this IEnumerable<string> texts) => texts.Select(u => u.ToLower());
		//public static IEnumerable<string> ToUpper(this IEnumerable<string> texts) => texts.Select(u => u.ToUpper());
		//public static IEnumerable<string> ToASCII(this IEnumerable<string> texts, string replaceInvalidWith = "~") => texts.Select(u => u.ToASCII(replaceInvalidWith));
		//public static IEnumerable<string> RemoveDiacritics(this IEnumerable<string> text) => text.Select(t => t.RemoveDiacritics());
	}






	public delegate T ValueAdjuster<T>(T value);

	public static class Ext_ValueAdjuster {
		internal static IEnumerable<V> Apply<V>(this ValueAdjuster<V> f, IEnumerable<V> collection) {
			return collection.Select(y => f(y));
		}
	}






	public static class Ext_FieldInfo {
		public static bool IsConst(this FieldInfo fi) => fi.IsLiteral && !fi.IsInitOnly;
		public static bool IsReadOnly(this FieldInfo fi) => fi.IsLiteral && fi.IsInitOnly;
	}






	public static class Ext_Type {
		public static T[] GetConstants<T>(this Type type) {
			var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
			return fields.Where(fi => fi.IsConst() && fi.FieldType == typeof(T))
			.Select(fi => (T)fi.GetValue(null)).ToArray();
		}
	}
}
