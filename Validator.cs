﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Ps = Limcap.LightValidator.Subject<string>;

namespace Limcap.LightValidator {

	/// <summary>
	///	Provides validation for any object and its members.
	/// </summary>
	public class Validator {

		// Current Subject fields
		internal string _subjectName;
		internal dynamic _subjectValue;
		internal dynamic _subjectEqualizer;
		internal bool _skipChecks;
		internal bool _subjectIsValid;
		internal ValidationResult _subjectResult;



		public List<ValidationResult> Results { get; private set; }
		public string LastError => Results.LastOrDefault().Messages?.LastOrDefault();
		public bool LastTestHasPassed { get; internal set; }



		public void Reset() {
			Results = new List<ValidationResult>();
			_subjectName = null;
			_subjectValue = null;
			_subjectEqualizer = null;
			_subjectIsValid = false;
			_skipChecks = false;
			_subjectResult = new ValidationResult();
			LastTestHasPassed = true;
		}



		public Subject<dynamic> Subject(string name) => ValidatorExtensions.Subject<dynamic>(this, name, null);
		public Subject<string> Subject(string name, string value) => ValidatorExtensions.Subject(this, name, value);
		public Subject<IEnumerable<V>> Subject<V>(string name, IEnumerable<V> value) => ValidatorExtensions.Subject(this, name, value);



		internal void AddErrorMessage(string msg) {
			if (_subjectIsValid) {
				Results = Results ?? new List<ValidationResult>();
				LoadResultInstance();
			}
			_subjectResult.Messages.Add(msg);
		}



		private void LoadResultInstance() {
			for (int i = 0; i < Results.Count; i++) {
				if (Results[i].Subject == _subjectName) {
					_subjectResult = Results[i];
					return;
				}
			}
			_subjectResult = new ValidationResult(_subjectName);
			Results.Add(_subjectResult);
		}
	}






	public static class ValidatorExtensions {
		// Esse nétodo precisa ser de extensão senão ele tem precedência na resolução de overloading
		// do linter sobre o Subject<IEnumerable<V>>, o que faz com que as chamadas do método Subject com
		// um value que seja IEnumerable seja identificado incorretamente pelo linter, e então as chamadas
		// para os métodos de extensão de Subject<IEnumerable<V>> ficam marcados como erro no linter.
		// Sendo extensão, ele cai na hierarquia de resolução resolvendo o problema.
		public static Subject<V> Subject<V>(this Validator v, string name, V value) {
			v._subjectName = name;
			v._subjectValue = value;
			v._subjectIsValid = true;
			v._skipChecks = false;
			v._subjectResult = default;
			v._subjectEqualizer = true;
			v.LastTestHasPassed = true;
			return new Subject<V>(v);
		}
	}






	public struct Subject<V> {

		internal Subject(Validator v) { this.v = v; }



		private Validator v;



		public string Name { get => v._subjectName; private set => v._subjectName = value; }
		public V Value { get => IsRightValueType ? v._subjectValue : default(V); internal set => v._subjectValue = value; }
		public bool IsValid => v._subjectIsValid;
		public ValidationResult Result => v._subjectResult;
		private bool IsRightValueType => v._subjectValue == null && default(V) == null || v._subjectValue.GetType() == typeof(V);



		public Subject<V> UseEqualizer(ValueAdjuster<V> equalizer) { v._subjectEqualizer = equalizer; return this; }

		public Subject<V> UseEqualizer(StrOp eq) { v._subjectEqualizer = eq; return this; }



		private dynamic Equalize(dynamic dynVal, dynamic dynEq) {
			if (dynVal == null || dynEq == null) return null;
			if (dynVal is string str && dynEq is StrOp op)
				dynVal = op.Apply(str);
			else if (dynVal is V val && dynEq is ValueAdjuster<V> adj)
				dynVal = adj(val);
			else if (dynVal is IEnumerable<string> strs && dynEq is StrOp op2)
				dynVal = strs.Select(o => op2.Apply(o));
			else if (dynVal is IEnumerable<V> vals && dynEq is ValueAdjuster<V> adj2)
				dynVal = vals.Select(o => adj2(o));
			return dynVal;
		}



		public Subject<T> Cast<T>() => new Subject<T>(v);

		public Subject<V> Alter(V value) { Value = value; return this; }



		public Subject<T> To<T>(Func<V, T> converter, string msg = null) {
			var newSubject = new Subject<T>(v);
			try { v._subjectValue = converter(v._subjectValue); }
			catch (Exception ex) {
				var exInfo = $"[{ex.GetType().Name}: {ex.Message}]";
				v.AddErrorMessage(msg ?? DefaultConvertMsg<T>(exInfo));
				v._subjectValue = default(T);
				v._skipChecks = true;
			}
			return newSubject;
		}



		public Subject<T> To<T, S>(Func<V, S, T> converter, S supplement, string msg = null) {
			var newSubject = new Subject<T>(v);
			try { v._subjectValue = converter(v._subjectValue, supplement); }
			catch (Exception ex) {
				var exInfo = $"{ex.GetType().Name}: {ex.Message}";
				v.AddErrorMessage(msg ?? DefaultConvertMsg<T>(exInfo));
				v._subjectValue = default(S);
				v._skipChecks = true;
			}
			return newSubject;
		}



		static string DefaultConvertMsg<T>(string info) => $"Não é um valor válido para o tipo '{typeof(T).Name}' - [{info}]";



		public Subject<V> Check(string failureMessage, ValidationTest<V> test) {
			if (v._skipChecks || !v.LastTestHasPassed) return this;
			try {
				//var value = Equalize(v._subjectValue, v._subjectEqualizer);
				var success = test(v._subjectValue);
				if (!success) v.AddErrorMessage(failureMessage);
				v.LastTestHasPassed = success;
				v._skipChecks = !success;
			}
			catch (Exception ex) {
				v.AddErrorMessage("[Exception] " + ex.Message);
				v.LastTestHasPassed = false;
				v._skipChecks = true;
			}
			return this;
		}



		public Subject<V> Check<A>(string failureMessage, ValidationTest<V, A> test, A testArg) {
			if (v._skipChecks || !v.LastTestHasPassed) return this;
			try {
				//var value = Equalize(v._subjectValue, v._subjectEqualizer);
				//testArg = Equalize(testArg, v._subjectEqualizer);
				var success = test(v._subjectValue, testArg);
				if (!success) v.AddErrorMessage(failureMessage);
				v.LastTestHasPassed = success;
				v._skipChecks = !success;
			}
			catch (Exception ex) {
				v.AddErrorMessage("[Exception] " + ex.Message);
				v.LastTestHasPassed = false;
				v._skipChecks = true;
			}
			return this;
		}



		public Subject<V> Check(string failureMessage, bool test) {
			if (!v.LastTestHasPassed) return this;
			v.LastTestHasPassed = test;
			v._skipChecks = !test;
			if (!test) v.AddErrorMessage(failureMessage);
			return this;
		}



		public Subject<V> Check(bool test) => Check("Valor inválido", test);



		public Subject<V> Skip(bool condition) { v._skipChecks = !condition; return this; }
		public Subject<V> Skip(Func<V, bool> condition) { v._skipChecks = !condition(Value); return this; }
	}






	[DebuggerDisplay("{DD(), nq}")]
	public struct ValidationResult {
		public ValidationResult(string subject) { Subject = subject; Messages = new List<string>(); }
		public readonly string Subject;
		public readonly List<string> Messages;
		#if DEBUG
		private string DD() {
			var str1 = nameof(Subject) + (Subject is null ? "=null" : $"=\"{Subject}\"");
			var str2 = nameof(Messages) + (Messages is null ? $"=null" : $".Count={Messages.Count}");
			return $"{str1}, {str2}";
		}
		#endif
	}






	public delegate bool ValidationTest<V>(V value);
	public delegate bool ValidationTest<V, R>(V value, R allowed = default);
	public delegate void ValidationScript(Validator v);






	internal static class Tests {
		public static bool NotNull<V>(V x) => x != null;
		public static bool NotEmpty<V>(IEnumerable<V> x) => x != null && x.Count() > 0;
		public static bool NotBlank(string x) => !string.IsNullOrWhiteSpace(x);
		public static bool IsMatch(string x, string a) => x != null && Regex.IsMatch(x, a);
		public static bool In<V>(V x, IEnumerable<V> a) => x != null && a.Contains(x);
		public static bool Equals<V>(V x, V a) where V : IEquatable<V> => x != null && x.Equals(a);
		public static bool MaxLength<V>(IEnumerable<V> x, int t) => x == null || x.Count() <= t;
		public static bool MinLength<V>(IEnumerable<V> x, int t) => x != null && x.Count() >= t;
		public static bool Length<V>(IEnumerable<V> x, int t) => x != null && x.Count() == t;
		public static bool IsAtLeast<V>(V x, V t) where V : IComparable<V> => x != null && x.CompareTo(t) >= 0;
		public static bool IsAtMost<V>(V x, V t) where V : IComparable<V> => x == null || x.CompareTo(t) <= 0;
		public static bool Exactly<V>(V x, V t) where V : IComparable<V> => x != null && x.CompareTo(t) == 0;
		public static bool Min<V>(IEnumerable<V> x, int t) => x != null && x.Count() >= t;
		public static bool Max<V>(IEnumerable<V> x, int t) => x == null || x.Count() <= t;
		public static bool Exactly<V>(IEnumerable<V> x, int t) => x != null && x.Count() == t;
		public static bool IsEmail(string x) => Regex.IsMatch(x, @"^\w+([.-]?\w+)*@\w+([.-]?\w+)*(\.\w{2,3})+$");
		public static bool IsDigitsOnly(string x) => x != null && x.All(y => char.IsDigit(y));
	}






	public static class SubjectExtensions_Checks {
		// generic
		public static Subject<V> IsNotNull<V>(this Subject<V> p, string msg = null) {
			p.Check(msg ?? $"Não pode ser nulo", Tests.NotNull); return p;
		}
		public static Subject<V> IsIn<V>(this Subject<V> p, IEnumerable<V> options, string msg = null) {
			p.Check(msg ?? $"Não é um valor válido", Tests.In, options); return p;
		}
		public static Subject<V> IsIn<V>(this Subject<V> p, params V[] options) {
			p.Check($"Não é um opção válida", Tests.In, options); return p;
		}

		// IEquatable
		public static Subject<V> IsEquals<V>(this Subject<V> p, V value, string msg = null) where V : IEquatable<V> {
			p.Check(msg ?? $"Deve ser {value}", Tests.Equals, value); return p;
		}

		// IComparable
		public static Subject<V> IsAtLeast<V>(this Subject<V> p, V minValue, string msg = null) where V : IComparable<V> {
			p.Check(msg ?? $"Não pode ser menor que {minValue}", Tests.IsAtLeast, minValue); return p;
		}
		public static Subject<V> IsAtMost<V>(this Subject<V> p, V maxValue, string msg = null) where V : IComparable<V> {
			p.Check(msg ?? $"Não pode ser maior que {maxValue}", Tests.IsAtMost, maxValue); return p;
		}
		public static Subject<V> Is<V>(this Subject<V> p, V value, string msg = null) where V : IComparable<V> {
			p.Check(msg ?? $"Deve ser exatamente {value}", Tests.Exactly, value); return p;
		}

		// IEnumerable
		public static Subject<IEnumerable<V>> IsNotEmpty<V>(this Subject<IEnumerable<V>> p, string msg = null) {
			p.Check(msg ?? $"Não está preenchido", Tests.NotEmpty); return p;
		}
		public static Subject<IEnumerable<V>> HasLength<V>(this Subject<IEnumerable<V>> p, int length, string msg = null) {
			p.Check(msg ?? $"Deve ter exatamente {length} itens", Tests.Length, length); return p;
		}
		public static Subject<IEnumerable<V>> HasMinLength<V>(this Subject<IEnumerable<V>> p, int length, string msg = null) {
			p.Check(msg ?? $"Não pode ser menor que {length} itens", Tests.MinLength, length); return p;
		}
		public static Subject<IEnumerable<V>> HasMaxLength<V>(this Subject<IEnumerable<V>> p, int length, string msg = null) {
			p.Check(msg ?? $"Não pode ser maior que {length} itens", Tests.MaxLength, length); return p;
		}

		// int
		public static Subject<int> IsAtLeast(this Subject<int> p, int number, string msg = null) {
			p.Check(msg ?? $"Não pode ser menor que {number}", Tests.IsAtLeast, number); return p;
		}
		public static Subject<int> IsAtMost(this Subject<int> p, int number, string msg = null) {
			p.Check(msg ?? $"Não pode ser maior que {number}", Tests.IsAtMost, number); return p;
		}
		public static Subject<int> Is(this Subject<int> p, int number, string msg = null) {
			p.Check(msg ?? $"Deve ser exatamente {number}", Tests.Exactly, number); return p;
		}

		// string
		public static Subject<string> IsNotEmpty(this Subject<string> p, string msg = null) {
			p.Check(msg ?? $"Não está preenchido", Tests.NotEmpty); return p;
		}
		public static Subject<string> IsNotBlank(this Subject<string> p, string msg = null) {
			p.Check(msg ?? $"Não está preenchido", Tests.NotEmpty); return p;
		}
		public static Subject<string> HasLength(this Subject<string> p, int length, string msg = null) {
			p.Check(msg ?? $"Deve ter exatamente {length} caracteres", Tests.Length, length); return p;
		}
		public static Subject<string> HasMinLength(this Subject<string> p, int length, string msg = null) {
			p.Check(msg ?? $"Não pode ser menor que {length} caracteres", Tests.MinLength, length); return p;
		}
		public static Subject<string> HasMaxLength(this Subject<string> p, int length, string msg = null) {
			p.Check(msg ?? $"Não pode ser maior que {length} caracteres", Tests.MaxLength, length); return p;
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



	public static class SubjectExtensions_Conversions {
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

		//static Subject<N> _asT<N>(Ps p, Func<string,N> converter) => p.To(converter);
		static Subject<T> _to<T>(Ps p, Func<string, T> converter) => p.To(converter, _msgN<T>());
		static string _msgN<N>() => $"Não é um número válido do tipo '{typeof(N).Name}'";
		static DateTime _parseDT(string v, string f) => DateTime.ParseExact(v, f, CultureInfo.CurrentCulture);
	}






	public static class SubjectExtensions_General {
		public static Subject<T> GetValue<T>(this Subject<T> p, out T variable) { variable = p.Value; return p; }
		public static Subject<IEnumerable<T>> SkipIfBlank<T>(this Subject<IEnumerable<T>> p) { p.Skip(p.Value.Any()); return p; }
		public static Ps SkipIfBlank(this Ps p) { p.Skip(!p.Value.IsBlank()); return p; }
	}






	public static class SubjectExtensions_StringOps {
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






	public static class StringExtensions {

		public static string RemoveChars(this string str, string chars) {
			return Regex.Replace(str, Regex.Escape(chars), "");
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






	public enum StrOp {
		Trim = 1, ToLower = 2, ToUpper = 4, RemoveDiacritics = 8, ToASCII = 16,
		RemoveNonASCII = 32, RemoveNonWord = 64, RemoveNonDigits = 128,
		Normalize = 11, NormalizeASCII = 27
	}

	public static class StrOpExtensions {
		public static string Apply(this StrOp op, string x) {
			if (op.HasFlag(StrOp.Trim)) { x = x.Trim(); }
			if (op.HasFlag(StrOp.ToLower)) { x = x.ToUpper(); }
			if (op.HasFlag(StrOp.ToUpper)) { x = x.ToUpper(); }
			if (op.HasFlag(StrOp.RemoveDiacritics)) { x = x.RemoveDiacritics(); }
			if (op.HasFlag(StrOp.ToASCII)) { x = x.ToASCII(); }
			return x;
		}
	}






	public delegate T ValueAdjuster<T>(T value);

	public static class ValueAdjusterExtensions {
		internal static IEnumerable<V> Apply<V>(this ValueAdjuster<V> f, IEnumerable<V> collection) {
			return collection.Select(y => f(y));
		}
	}
}
