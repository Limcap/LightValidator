### Ver 0.5.1
- Added feature to convert Report and Logs to Json: (Report.ToJson(), Log.ToJson(),
  Log.SetJsonPropertyNames())
- Rename Element.SetValue to Element.NewValue and add overloads
- Add Element extension IsDefinedInEnum

### Ver 0.5.0
- Bugfixes
- Ranamed Subject class to Element
- New concept and feature Report: each failed validation test creates a Log in the Validator's Report
- Created new Scope concept and feature. When validation, you can define a scope, and when a Log is 
  created in the Report it will have the Scope, the Element name and the Message. 
- Improvements in Report class

### Ver 0.4.0
- Bugfixes
- Rename Input class to Subject
- Removed object parameter from Validator contructor. Was used to define the object beeing validated
  This concept has been removed
- Removed Equalizer feature. Any amends to values should be done externally
- Removed SkipNextCheck feature. (Disaproved) 
- Rename ValidationResult class to Log
- Improved the Subject.Cast feature
- New string conversions (ToDate)
- New FieldInfo extensions (IsConst, IsReadOnly)
- New Type extensions (GetConst)
- New EmailAddress struct
- new Test added: IsNull, IsEmpty, IsBlank
- Renamed Subject.LocaVar to Subject.GetCurrentValue
- Renamed Suject.Alter to Subject.SetValue

### Ver 0.3.3
- Fix property and variable names
- Rename Param class to Input
- New extension and conversion methods
- New Input class function to alter value (Input.Alter)
- New Input class function to export current value (Input.LovalVar)
- New string methods (Crop, Replace, RemoveChars)
- New feature SkipNextChecks to skip every next check if condition applies
- New feature SkipIfBlank to skip every next check if value is blank

### Ver 0.3.2
- Bugfixes and a few improvements in Tests
- Fix typos
- Fix method signature

### Ver 0.3.1
- Classes were renamed for the better
- Field method is now Param
- Can now test a Param without supplying its value. However there is no pre-defined test for this mode, you'll have to dupply yout own test
- Some changes in some access modifiers (public/internal)

### Ver 0.3.0
- Extensive refactoring
- Better code readability
- Estructural improvement in Tests and Test delegates
- More generic tests, increasing the amout of possible uses for each

### Validator 0.2.0.3:
- Validator e validation foram fundidos em um so objeto.
- A classe Field foi absorvida pela Validator para aumentar a performace, pois assim não será necessário criar um novo objeto no heap a cada chamada da função Field;
- Novo struct Tester ocupou lugar do antigo Field. Ele possui um único membro que é uma referência para o próprio Validator de forma a permitir que o struct seja retornado no diversas vezes durante a validação sem ocupar muito espaço do stack e aumentando a performance.
- Novo ValidationText[V,C] permite que faça-se testes com parâmetros possam ser predefinidos em vez de ser delegates gerados em runtime
