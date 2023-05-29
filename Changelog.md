### Validator 0.2.0.3:
- Validator e validation foram fundidos em um so objeto.
- A classe Field foi absorvida pela Validator para aumentar a performace, pois assim não será necessário criar um novo objeto no heap a cada chamada da função Field;
- Novo struct Tester ocupou lugar do antigo Field. Ele possui um único membro que é uma referência para o próprio Validator de forma a permitir que o struct seja retornado no diversas vezes durante a validação sem ocupar muito espaço do stack e aumentando a performance.
- Novo ValidationText[V,C] permite que faça-se testes com parâmetros possam ser predefinidos em vez de ser delegates gerados em runtime
