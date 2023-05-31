using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Ps = Limcap.LightValidator.Input<string>;

namespace Limcap.LightValidator {

	/// <summary>
	///	Provides validation for any object and its members.
	/// </summary>
	public class Validator {
		public Validator(dynamic obj = null) {
			Object = obj;
		}



		// Current Input fields
		internal string _inputName;
		internal dynamic _inputValue;
		internal dynamic _inputEqualizer;
		internal bool _skipChecks;
		internal bool _inputIsValid;
		internal ValidationResult _inputResult;



		public dynamic Object { get; private set; }
		public List<ValidationResult> Results { get; private set; }
		public string LastError => Results.LastOrDefault().Messages?.LastOrDefault();
		public bool LastTestHasPassed { get; internal set; }



		public void Reset(dynamic obj = null) {
			Object = obj;
			Results = null;
			_inputName = null;
			_inputValue = null;
			_inputEqualizer = null;
			_inputIsValid = false;
			_skipChecks = false;
			_inputResult = new ValidationResult();
			LastTestHasPassed = true;
		}



		public Input<dynamic> Input(string name) => ValidatorExtensions.Input<dynamic>(this, name, null);
		public Input<string> Input(string name, string value) => ValidatorExtensions.Input(this, name, value);
		public Input<IEnumerable<V>> Input<V>(string name, IEnumerable<V> value) => ValidatorExtensions.Input(this, name, value);



		internal void AddErrorMessage(
		string msg) {
			if (_inputIsValid) {
				_inputResult = new ValidationResult(_inputName);
				InitializeResults();
				Results.Add(_inputResult);
				_inputIsValid = false;
			}
			_inputResult.Messages.Add(msg);
		}



		internal void InitializeResults() {
			Results = Results ?? new List<ValidationResult>();
		}



		internal void RemoveEmptyResults() {
			Results?.RemoveAll(x => x.Messages.Count == 0);
		}
	}






	public static class ValidatorExtensions {
		// Esse nétodo precisa ser de extensão senão ele tem precedência na resolução de overloading
		// do linter sobre o Input<IEnumerable<V>>, o que faz com que as chamadas do método Input com
		// um value que seja IEnumerable seja identificado incorretamente pelo linter, e então as chamadas
		// para os métodos de extensão de Input<IEnumerable<V>> ficam marcados como erro no linter.
		// Sendo extensão, ele cai na hierarquia de resolução resolvendo o problema.
		public static Input<V> Input<V>(this Validator v, string name, V value) {
			v._inputName = name;
			v._inputValue = value;
			v._inputIsValid = true;
			v._skipChecks = false;
			//v._inputResult = new ValidationResult();
			v._inputEqualizer = true;
			v.LastTestHasPassed = true;
			return new Input<V>(v);
		}
	}






	public struct Input<V> {

		internal Input(Validator v) { this.v = v; }



		private Validator v;



		public string Name { get => v._inputName; set => v._inputName = value; }
		public V Value { get => v._inputValue; set => v._inputValue = value; }
		public bool IsValid => v._inputIsValid;
		public ValidationResult Result => v._inputResult;



		public Input<V> UseEqualizer(ValueAdjuster<V> equalizer) { v._inputEqualizer = equalizer; return this; }

		public Input<V> UseEqualizer(StrOp eq) { v._inputEqualizer = eq; return this; }



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



		public Input<T> Cast<T>() => new Input<T>(v);

		public Input<V> Alter(V value) { Value = value; return this; }



		public Input<T> To<T>(Func<V, T> converter, string msg = null) {
			var newInput = new Input<T>(v);
			try { v._inputValue = converter(v._inputValue); }
			catch (Exception ex) {
				var exInfo = $"[{ex.GetType().Name}: {ex.Message}]";
				v.AddErrorMessage(msg ?? DefaultConvertMsg<T>(exInfo));
				v._skipChecks = true;
			}
			return newInput;
		}



		public Input<T> To<T, S>(Func<V, S, T> converter, S supplement, string msg = null) {
			var newInput = new Input<T>(v);
			try { v._inputValue = converter(v._inputValue, supplement); }
			catch (Exception ex) {
				var exInfo = $"{ex.GetType().Name}: {ex.Message}";
				v.AddErrorMessage(msg ?? DefaultConvertMsg<T>(exInfo));
				v._skipChecks = true;
			}
			return newInput;
		}



		static string DefaultConvertMsg<T>(string info) => $"Não é convertível para o formato esperado ({typeof(T).Name}). [{info}]";



		public Input<V> Check(string failureMessage, ValidationTest<V> test) {
			try {
				var value = Equalize(v._inputValue, v._inputEqualizer);
				var success = test(value);
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



		public Input<V> Check<A>(string failureMessage, ValidationTest<V, A> test, A testArg) {
			if (!v.LastTestHasPassed) return this;
			try {
				var value = Equalize(v._inputValue, v._inputEqualizer);
				testArg = Equalize(testArg, v._inputEqualizer);
				var success = test(value, testArg);
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



		public Input<V> Check(string failureMessage, bool test) {
			if (!v.LastTestHasPassed) return this;
			v.LastTestHasPassed = test;
			v._skipChecks = !test;
			if (!test) v.AddErrorMessage(failureMessage);
			return this;
		}



		public Input<V> Check(bool test) => Check("Valor inválido", test);



		public Input<V> Skip(bool condition) { v._skipChecks = !condition; return this; }
		public Input<V> Skip(Func<V, bool> condition) { v._skipChecks = !condition(Value); return this; }
	}






	[DebuggerDisplay("{DD(), nq")]
	public struct ValidationResult {
		public ValidationResult(string inputName) { Input = inputName; Messages = new List<string>(); }
		public readonly string Input;
		public readonly List<string> Messages;
#if DEBUG
		public string DD() => $"{nameof(Input)}=\"{Input}\", {nameof(Messages)}.Count={Messages.Count}";
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






	public static class InputExtensions_Checks {
		// generic
		public static Input<V> IsNotNull<V>(this Input<V> p, string msg = null) {
			p.Check(msg ?? $"Não pode ser nulo", Tests.NotNull); return p;
		}
		public static Input<V> IsIn<V>(this Input<V> p, IEnumerable<V> group, string msg = null) {
			p.Check(msg ?? $"Não é um valor válido", Tests.In, group); return p;
		}
		public static Input<V> IsIn<V>(this Input<V> p, string msg, params V[] options) {
			p.Check(msg ?? $"Não é um opção válida", Tests.In, options); return p;
		}
		public static Input<V> IsIn<V>(this Input<V> p, params V[] options) {
			p.Check($"Não é um opção válida", Tests.In, options); return p;
		}

		// IEquatable
		public static Input<V> IsEquals<V>(this Input<V> p, V value, string msg = null) where V : IEquatable<V> {
			p.Check(msg ?? $"Deve ser {value}", Tests.Equals, value); return p;
		}

		// IComparable
		public static Input<V> IsAtLeast<V>(this Input<V> p, V minValue, string msg = null) where V : IComparable<V> {
			p.Check(msg ?? $"Não pode ser menor que {minValue}", Tests.IsAtLeast, minValue); return p;
		}
		public static Input<V> IsAtMost<V>(this Input<V> p, V maxValue, string msg = null) where V : IComparable<V> {
			p.Check(msg ?? $"Não pode ser maior que {maxValue}", Tests.IsAtMost, maxValue); return p;
		}
		public static Input<V> Is<V>(this Input<V> p, V value, string msg = null) where V : IComparable<V> {
			p.Check(msg ?? $"Deve ser exatamente {value}", Tests.Exactly, value); return p;
		}

		// IEnumerable
		public static Input<IEnumerable<V>> IsNotEmpty<V>(this Input<IEnumerable<V>> p, string msg = null) {
			p.Check(msg ?? $"Não está preenchido", Tests.NotEmpty); return p;
		}
		public static Input<IEnumerable<V>> HasLength<V>(this Input<IEnumerable<V>> p, int length, string msg = null) {
			p.Check(msg ?? $"Deve ter exatamente {length} itens", Tests.Length, length); return p;
		}
		public static Input<IEnumerable<V>> HasMinLength<V>(this Input<IEnumerable<V>> p, int length, string msg = null) {
			p.Check(msg ?? $"Não pode ser menor que {length} itens", Tests.MinLength, length); return p;
		}
		public static Input<IEnumerable<V>> HasMaxLength<V>(this Input<IEnumerable<V>> p, int length, string msg = null) {
			p.Check(msg ?? $"Não pode ser maior que {length} itens", Tests.MaxLength, length); return p;
		}

		// int
		public static Input<int> IsAtLeast(this Input<int> p, int number, string msg = null) {
			p.Check(msg ?? $"Não pode ser menor que {number}", Tests.IsAtLeast, number); return p;
		}
		public static Input<int> IsAtMost(this Input<int> p, int number, string msg = null) {
			p.Check(msg ?? $"Não pode ser maior que {number}", Tests.IsAtMost, number); return p;
		}
		public static Input<int> Is(this Input<int> p, int number, string msg = null) {
			p.Check(msg ?? $"Deve ser exatamente {number}", Tests.Exactly, number); return p;
		}

		// string
		public static Input<string> IsNotEmpty(this Input<string> p, string msg = null) {
			p.Check(msg ?? $"Não está preenchido", Tests.NotEmpty); return p;
		}
		public static Input<string> IsNotBlank(this Input<string> p, string msg = null) {
			p.Check(msg ?? $"Não está preenchido", Tests.NotEmpty); return p;
		}
		public static Input<string> HasLength(this Input<string> p, int length, string msg = null) {
			p.Check(msg ?? $"Deve ter exatamente {length} caracteres", Tests.Length, length); return p;
		}
		public static Input<string> HasMinLength(this Input<string> p, int length, string msg = null) {
			p.Check(msg ?? $"Não pode ser menor que {length} caracteres", Tests.MinLength, length); return p;
		}
		public static Input<string> HasMaxLength(this Input<string> p, int length, string msg = null) {
			p.Check(msg ?? $"Não pode ser maior que {length} caracteres", Tests.MaxLength, length); return p;
		}
		public static Input<string> IsMatch(this Input<string> p, string pattern, string msg = null) {
			p.Check(msg ?? "Não é um valor aceito", Tests.IsMatch, pattern); return p;
		}
		public static Input<string> IsEmail(this Input<string> p, string msg = null) {
			p.Check(msg ?? "Não é um e-mail válido", Tests.IsEmail); return p;
		}
		public static Input<string> IsDigitsOnly(this Input<string> p, string msg = null) {
			p.Check(msg ?? "Deve conter somente digitos (0-9)", Tests.IsDigitsOnly); return p;
		}
	}



	public static class InputExtensions_Conversions {
		public static Ps AsString<T>(this Input<T> p) => p.To(o => o.ToString());
		public static Input<byte> AsByte(this Ps p) => _to(p, o => byte.Parse(o));
		public static Input<short> AsShort(this Ps p) => _to(p, o => short.Parse(o));
		public static Input<ushort> AsUshort(this Ps p) => _to(p, o => ushort.Parse(o));
		public static Input<int> AsInt(this Ps p) => _to(p, o => int.Parse(o));
		public static Input<uint> AsUint(this Ps p) => _to(p, o => uint.Parse(o));
		public static Input<long> AsLong(this Ps p) => _to(p, o => long.Parse(o));
		public static Input<ulong> AsUlong(this Ps p) => _to(p, o => ulong.Parse(o));
		public static Input<float> AsFloat(this Ps p) => _to(p, o => float.Parse(o));
		public static Input<decimal> AsDecimal(this Ps p) => _to(p, o => decimal.Parse(o));
		public static Input<double> AsDouble(this Ps p) => _to(p, o => double.Parse(o));
		public static Input<DateTime> AsDateTime(this Ps p, string format) => p.To(_parseDT, format);

		//static Input<N> _asT<N>(Ps p, Func<string,N> converter) => p.To(converter);
		static Input<T> _to<T>(Ps p, Func<string, T> converter) => p.To(converter, _msgN<T>());
		static string _msgN<N>() => $"Não é um número válido do tipo '{typeof(N).Name}'";
		static DateTime _parseDT(string v, string f) => DateTime.ParseExact(v, f, CultureInfo.CurrentCulture);
	}






	public static class InputExtensions_General {
		public static Input<T> GetValue<T>(this Input<T> p, out T variable) { variable = p.Value; return p; }
		public static Input<IEnumerable<T>> SkipIfBlank<T>(this Input<IEnumerable<T>> p) { p.Skip(p.Value.Any()); return p; }
		public static Ps SkipIfBlank(this Ps p) { p.Skip(!p.Value.IsBlank()); return p; }
	}






	public static class InputExtensions_StringOps {
		public static Ps Trim(this Ps p, char c = (char)0) { p.Value = c == 0 ? p.Value.Trim() : p.Value.Trim(c); return p; }
		public static Ps ToLower(this Ps p) { p.Value = p.Value?.ToLower(); return p; }
		public static Ps ToUpper(this Ps p) { p.Value = p.Value?.ToUpper(); return p; }
		public static Ps RemoveDiacritics(this Ps p) { p.Value = p.Value?.RemoveDiacritics(); return p; }
		public static Ps ToASCII(this Ps p) { p.Value = p.Value?.ToASCII(); return p; }
		public static Ps Crop(this Ps p, int startIndex, int length) { p.Value = p.Value?.Crop(startIndex, length); return p; }
		public static Ps Replace(this Ps p, string oldStr, string newStr) { p.Value = p.Value?.Replace(oldStr, newStr); return p; }
		public static Ps Replace(this Ps p, char oldChar, char newChar) { p.Value = p.Value?.Replace(oldChar, newChar); return p; }
		public static Ps Replace(this Ps p, params ValueTuple<char, char>[] pairs) { p.Value = p.Value.Replace(pairs); return p; }
	}






	public static class StringExtensions {

		public static string RemoveChars(this string str, string chars) {
			return Regex.Replace(str, Regex.Escape(chars), "");
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

		public static IEnumerable<string> Trim(this IEnumerable<string> texts) => texts.Select(u => u.Trim());
		public static IEnumerable<string> ToLower(this IEnumerable<string> texts) => texts.Select(u => u.ToLower());
		public static IEnumerable<string> ToUpper(this IEnumerable<string> texts) => texts.Select(u => u.ToUpper());
		public static IEnumerable<string> ToASCII(this IEnumerable<string> texts, string replaceInvalidWith = "~") => texts.Select(u => u.ToASCII(replaceInvalidWith));
		public static IEnumerable<string> RemoveDiacritics(this IEnumerable<string> text) => text.Select(t => t.RemoveDiacritics());
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
