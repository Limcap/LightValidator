using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Schema;

namespace Limcap.LightValidator {

	/// <summary>
	/// Novo validator 2.0:
	/// Validator e validation foram fundidos em um so objeto. O campo Field agora é só um DTO e o novo Tester ocupou seu lugar,
	/// como um struct com somente a referencia para o Validator de forma a evitar a criação de vários objetos em memória reservada.
	/// (Para que isso seja alcançado, o objeto Field deve ser dissolvido e seus membros absorvidos pelo Validator - na versao alpha 2).
	/// Novo ValidationText<V,C> permite que faça-se testes com parâmetros possam ser predefinidos em vez de ser delegates gerados em runtime
	/// </summary>
	/// <version>0.2.0.3-a1</version>
	public class Validator {
		public Validator( dynamic obj ) {
			Object = obj;
		}




		public dynamic Object { get; private set; }
		public List<ValidationResult> Results { get; private set; }
		internal Field CurentField { get; private set; }

		//public string CurrentFieldName { get; }
		//public dynamic CurrentFieldValue { get; }
		//public bool CurrentFieldIsValid { get; internal set; }
		//public ValidationResult? CurrentFieldResult { get; internal set; }
		//public string CurrentFieldLastResult => CurrentFieldResult?.ErrorMessages.LastOrDefault();





		public void Reset( dynamic obj = null ) {
			Object = obj;
			Results = null;
			CurentField = null;
		}




		internal void InitializeResults() {
			Results = Results ?? new List<ValidationResult>();
		}




		public Tester<V> Field<V>( string name, V value ) {
			CurentField =  new Field(name, value);
			var tester =  new Tester<V>(this);
			return tester;
		}




		internal void RemoveEmptyResults() {
			Results?.RemoveAll(x => x.ErrorMessages.Count == 0);
		}
	}







	public struct Tester<V> {
		internal Tester(Validator v) { this.v=v; }
		Validator v;


		public Tester<V> Test( string msg, ValidationTest<V> test ) {
			try {
				var success = test(v.CurentField.Value);
				if (!success) AddErrorMessage(msg);
			}
			catch (Exception ex) {
				AddErrorMessage("[Exception] " + ex.Message);
			}
			return this;
		}




		public Tester<V> Test<C>( string msg, ValidationTest<V,C> test, C compsarisonValue ) {
			try {
				var success = test(v.CurentField.Value, compsarisonValue);
				if (!success) AddErrorMessage(msg);
			}
			catch (Exception ex) {
				AddErrorMessage("[Exception] " + ex.Message);
			}
			return this;
		}




		public Tester<V> Test( string msg, bool success ) {
			if (!success) AddErrorMessage(msg);
			return this;
		}




		public Tester<V> Test( string msg, ValidationRule<V> rule ) {
			try {
				var success = rule.Test(v.CurentField.Value);
				if (!success) AddErrorMessage(rule.FailureMessage);
			}
			catch (Exception ex) {
				AddErrorMessage("[Exception] " + ex.Message);
			}
			return this;
		}




		public void AddErrorMessage( string msg ) {
			if (v.CurentField.IsValid) {
				v.CurentField.Result = new ValidationResult(v.CurentField.Name);
				v.InitializeResults();
				v.Results.Add(v.CurentField.Result.Value);
				v.CurentField.IsValid = false;
			}
			v.CurentField.Result?.ErrorMessages.Add(msg);
		}
	}






	public class Field {
		internal Field( string name, dynamic value ) {
			Name = name;
			Value = value;
			IsValid = true;
			Result = null;
		}


		public string Name { get; }
		public dynamic Value { get; }
		public bool IsValid { get; internal set; }
		public ValidationResult? Result { get; internal set; }
		public string LastResult => Result?.ErrorMessages.LastOrDefault();
	}








	[DebuggerDisplay("{DD(), nq")]
	public struct ValidationResult {
		public ValidationResult( string fieldName ) { FieldName=fieldName; ErrorMessages = new List<string>(); }
		public readonly string FieldName;
		public readonly List<string> ErrorMessages;
		#if DEBUG
		public string DD() => $"{nameof(FieldName)}=\"{FieldName}\", {nameof(ErrorMessages)}.Count={ErrorMessages.Count}";
		#endif
	}








	public struct ValidationRule<V> {
		public ValidationRule( string failureMessage, ValidationTest<V> test ) {
			Test = test;
			FailureMessage = failureMessage;
		}
		public ValidationTest<V> Test;
		public string FailureMessage;
	}








	public static class ValidationExtensions {
		public static class Tests {
			public static readonly ValidationTest<object> NotNull = x => x != null;
			public static readonly ValidationTest<string> NotEmpty = x => !string.IsNullOrWhiteSpace(x);
			public static readonly ValidationTest<string, int> MaxLength = (x,t) => x.Length <= t;
			public static readonly ValidationTest<string, int> MinLength = (x,t) => x.Length >= t;
			public static readonly ValidationTest<decimal, decimal> Max = (x,t) => x <= t;
			public static readonly ValidationTest<decimal, decimal> Min = (x,t) => x >= t;
			public static readonly ValidationTest<decimal, decimal> Exact = (x,t) => x == t;
		}

		public static Tester<object> NotNull( this Tester<object> field, string msg = null ) {
			field.Test(msg??$"Não pode ser nulo", Tests.NotNull); return field;
		}
		public static Tester<string> NotEmpty( this Tester<string> field, string msg = null ) {
			field.Test(msg??$"Não está preenchido", Tests.NotEmpty); return field;
		}
		public static Tester<string> MaxLength( this Tester<string> field, int target, string msg = null ) {
			field.Test(msg??$"Não pode ser maior que {target} caracteres", Tests.MaxLength, target); return field;
		}
		public static Tester<string> MinLength( this Tester<string> field, int target, string msg = null ) {
			field.Test(msg??$"Não pode ser menor que {target} caracteres", Tests.MinLength, target); return field;
		}
		public static Tester<decimal> Max( this Tester<decimal> field, decimal target, string msg = null ) {
			field.Test(msg??$"Não pode ser maior que {target}", Tests.Max, target); return field;
		}
		public static Tester<decimal> Min( this Tester<decimal> field, decimal target, string msg = null ) {
			field.Test(msg??$"Não pode ser menor que {target}", Tests.Min, target); return field;
		}
		public static Tester<decimal> Exact( this Tester<decimal> field, decimal target, string msg = null ) {
			field.Test(msg??$"Deve ser {target}", Tests.Exact, target); return field;
		}
	}








	public delegate bool ValidationTest<V>( V value );
	public delegate bool ValidationTest<V,C>( V value, C value2 );
	//public delegate string ValidationNamer( string originalName, dynamic validationSubject );
	public delegate void ValidationScript( Validator v );
}
