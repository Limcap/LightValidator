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
