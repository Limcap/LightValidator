using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Schema;

namespace Limcap.LightValidator {
	public class Validator {
		public ValidationNamer FieldNamer { get; set; }




		public void Validate( dynamic obj, out Validation validation ) {
			validation = new Validation(obj, FieldNamer);
		}




		public List<ValidationResult> Validate( dynamic obj, ValidationScript script ) {
			var v = new Validation(obj, FieldNamer);
			script(v);
			return v.Results;
		}
	}








	public class Validation {
		public Validation( dynamic obj, ValidationNamer fieldNamer ) {
			Subject = obj;
			FieldNamer = fieldNamer;
			//Results = new List<ValidationResults>()
		}




		public readonly dynamic Subject;
		public readonly ValidationNamer FieldNamer;
		public List<ValidationResult> Results;
		public void InitializeResults() => Results = Results ?? new List<ValidationResult>();




		public ValidationField<V> Field<V>( string name, V value ) {
			var tester =  new ValidationField<V>(name, value, this);
			//Results.Add(tester.Result);
			return tester;
		}




		internal void RemoveEmptyResults() {
			Results?.RemoveAll(x => x.ErrorMessages.Count == 0);
		}
	}








	public class ValidationField<V> {
		public ValidationField( string name, V value, Validation validation ) {
			v = validation;
			originalName = name;
			//Subject = validation.Subject;
			//FieldNamer = validation.FieldNamer;
			//Results = validation.Results;
			Value = value;
			IsValid = true;
		}


		private readonly string originalName;
		private readonly Validation v;
		public ValidationResult Result { get; private set; }
		public V Value { get; }
		public string LastResult {
			get {
				return Result.ErrorMessages.LastOrDefault();
			}
		}
		public bool IsValid { get; private set; }




		public void AddErrorMessage( string msg ) {
			if (IsValid) {
				var name = v.FieldNamer?.Invoke(originalName, v.Subject) ?? originalName;
				Result = new ValidationResult(name);
				v.InitializeResults();
				v.Results.Add(Result);
				IsValid = false;
			}
			Result.ErrorMessages.Add(msg);
		}




		public ValidationField<V> Test( string msg, ValidationTest<V> test ) {
			try {
				var success = test(Value);
				if (!success) AddErrorMessage(msg);
			}
			catch (Exception ex) {
				AddErrorMessage("[Exception] " + ex.Message);
			}
			return this;
		}




		public ValidationField<V> Test( string msg, bool success ) {
			if (!success) AddErrorMessage(msg);
			return this;
		}




		public ValidationField<V> Test( string msg, ValidationRule<V> rule ) {
			try {
				var success = rule.Test(Value);
				if (!success) AddErrorMessage(rule.FailureMessage);
			}
			catch (Exception ex) {
				AddErrorMessage("[Exception] " + ex.Message);
			}
			return this;
		}
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
			public static readonly ValidationTest<object> IsNotNull = x => x != null;
			public static readonly ValidationTest<string> IsFilled= x => !string.IsNullOrWhiteSpace(x);
			public static ValidationTest<string> MaxLength( int length ) => x => x.Length <= length;
		}
		public static ValidationField<object> NotNull( this ValidationField<object> field, string msg = null ) {
			field.Test(msg??$"Não pode ser nulo", x => x != null); return field;
		}
		public static ValidationField<string> NotEmpty( this ValidationField<string> field, string msg = null ) {
			field.Test(msg??$"Não está preenchido", x => !string.IsNullOrWhiteSpace(x)); return field;
		}
		public static ValidationField<string> MaxLength( this ValidationField<string> field, int length, string msg = null ) {
			field.Test(msg??$"Não pode ser maior que {length} caracteres", x => x.Length <= length); return field;
		}
		public static ValidationField<string> MinLength( this ValidationField<string> field, int length, string msg = null ) {
			field.Test(msg??$"Não pode ser menor que {length} caracteres", x => x.Length >= length); return field;
		}
		public static ValidationField<int> Max( this ValidationField<int> field, decimal value, string msg = null ) {
			field.Test(msg??$"Não pode ser maior que {value}", x => x <= value); return field;
		}
		public static ValidationField<int> Min( this ValidationField<int> field, decimal value, string msg = null ) {
			field.Test(msg??$"Não pode ser menor que {value}", x => x >= value); return field;
		}
		public static ValidationField<int> Exact( this ValidationField<int> field, decimal value, string msg = null ) {
			field.Test(msg??$"Deve ser {value}", x => x == value); return field;
		}
	}








	public delegate bool ValidationTest<V>( V value );
	public delegate string ValidationNamer( string originalName, dynamic validationSubject );
	public delegate void ValidationScript( Validation v );
}
