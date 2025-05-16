import sys
import lib2to3
from lib2to3 import pytree
from lib2to3.pgen2 import driver

# Операторы в Python (ключевые слова и символы)
OPERATORS = {
    "+", "-", "*", "/", "=", "==", "!=", "<", ">", "<=", ">=", "and", "or", "not",
    "in", "is", "&", "|", "^", "~", ">>", "<<", "%", "//", "**", "@"
}
OPERATOR_KEYWORDS = {"if", "else", "for", "while", "break", "continue", "return", "def", "class"}

def parse_code(code):
    # Парсинг кода с помощью lib2to3
    try:
        d = driver.Driver(lib2to3.grammar, convert=pytree.Node)
        tree = d.parse_string(code + "\n")
    except Exception as e:
        print(f"Error: {str(e)}")
        return

    operators = set()
    operands = set()
    N1 = 0  # Общее количество операторов
    N2 = 0  # Общее количество операндов

    def traverse(node):
        nonlocal N1, N2
        if isinstance(node, pytree.Leaf):
            if node.type == lib2to3.grammar.token.NAME:
                value = str(node)
                if value in OPERATOR_KEYWORDS:
                    operators.add(value)
                    N1 += 1
                else:
                    operands.add(value)
                    N2 += 1
            elif node.type == lib2to3.grammar.token.OP:
                value = str(node)
                if value in OPERATORS:
                    operators.add(value)
                    N1 += 1

        if isinstance(node, pytree.Node):
            for child in node.children:
                traverse(child)

    traverse(tree)
    print(f"Операторы:{','.join(operators)}")
    print(f"Операнды:{','.join(operands)}")
    print(f"N1:{N1}")
    print(f"N2:{N2}")

if __name__ == "__main__":
    #Чтение кода из файла
    if len(sys.argv) != 2:
        print("Ошибка: Укажите путь к файлу в качестве аргумента")
        sys.exit(1)

    file_path = sys.argv[1]
    try:
        with open(file_path, "r", encoding="utf-8") as f:
            code = f.read()
        parse_code(code)
    except Exception as e:
        print(f"Ошибка: {str(e)}")