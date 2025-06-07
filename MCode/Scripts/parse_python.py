# Scripts/parse_python.py
import sys
import ast
import tokenize
import keyword # Для проверки, является ли имя ключевым словом, которое мы не считаем операндом (если оно уже оператор)

# Определяем, что считать операторами. Этот список можно и нужно расширять/уточнять
# для более точного соответствия классификации Холстеда.
# Включаем символы из описания проекта и общие ключевые слова.
# Этот список будет более важным, т.к. ast.NodeVisitor не дает строковое представление операторов напрямую.
# Мы будем классифицировать типы узлов AST.
PYTHON_OPERATORS_KEYWORDS = {
    # Арифметические и битовые
    '+', '-', '*', '**', '/', '//', '%', '@', '<<', '>>', '&', '|', '^', '~',
    # Сравнения и присваивания
    '<', '>', '<=', '>=', '==', '!=', '=',
    '+=', '-=', '*=', '/=', '//=', '%=', '@=', '&=', '|=', '^=', '>>=', '<<=', '**=',
    # Логические и принадлежности
    'and', 'or', 'not', 'is', 'in', 'await'
}
# Добавляем операторы из описания проекта: !, <> (хотя <> устарел, == != предпочтительнее)
PYTHON_OPERATORS_KEYWORDS.update(['()', '[]', '.']) 


class HalsteadMetricsVisitor(ast.NodeVisitor):
    def __init__(self):
        self.operators = set()
        self.operands = set()
        self.N1 = 0  # Total operators
        self.N2 = 0  # Total operands

    def _add_operator(self, op_name):
        self.operators.add(op_name)
        self.N1 += 1

    def _add_operand(self, op_name):
        # Проверяем, не является ли имя переменной/функции ключевым словом,
        # которое мы уже классифицировали как оператор (например 'if', 'for')
        if op_name not in PYTHON_OPERATORS_KEYWORDS:
            self.operands.add(str(op_name)) # Приводим к строке, на всякий случай (для чисел и т.п.)
            self.N2 += 1
        # else: # Если это ключевое слово-оператор, оно уже посчитано как оператор
        #     pass


    def visit_Name(self, node):
        # Имена переменных, функций, классов и т.д.
        # Исключаем ключевые слова, которые могут быть здесь, но считаются операторами (e.g., 'True', 'False', 'None')
        if node.id in ['True', 'False', 'None']: # Эти константы - операнды
             self._add_operand(node.id)
        elif keyword.iskeyword(node.id) and node.id in PYTHON_OPERATORS_KEYWORDS:
            # Некоторые ключевые слова (if, for) могут быть распознаны как Name, но являются операторами
            # Мы их обработаем в соответствующих узлах (e.g. visit_If, visit_For)
            # Здесь, если это ключевое слово-оператор, мы его не считаем операндом.
            pass # Уже посчитано или будет посчитано как оператор
        else:
            # Если это простое имя (переменная, функция), то это операнд
            self._add_operand(node.id)
        self.generic_visit(node)

    def visit_Constant(self, node): # Для Python 3.8+ (старые версии: Num, Str, Bytes, NameConstant)
        # Литералы: числа, строки, True, False, None
        self._add_operand(node.value) # node.value хранит само значение
        self.generic_visit(node)

    # Для Python < 3.8, если Constant не работает или нужно быть более специфичным
    def visit_Num(self, node): # Числа (устарело в 3.8, используйте Constant)
        self._add_operand(node.n)
        self.generic_visit(node)

    def visit_Str(self, node): # Строки (устарело в 3.8, используйте Constant)
        self._add_operand(node.s)
        self.generic_visit(node)

    def visit_Bytes(self, node): # Байтовые строки (устарело в 3.8)
        self._add_operand(node.s)
        self.generic_visit(node)
    
    def visit_NameConstant(self, node): # True, False, None (устарело в 3.8)
        self._add_operand(node.value)
        self.generic_visit(node)


    def visit_BinOp(self, node):
        # Бинарные операции: +, -, *, /, //, %, **, <<, >>, |, ^, &
        op_map = {
            ast.Add: '+', ast.Sub: '-', ast.Mult: '*', ast.Div: '/',
            ast.FloorDiv: '//', ast.Mod: '%', ast.Pow: '**',
            ast.LShift: '<<', ast.RShift: '>>', ast.BitOr: '|',
            ast.BitXor: '^', ast.BitAnd: '&', ast.MatMult: '@'
        }
        self._add_operator(op_map.get(type(node.op), type(node.op).__name__))
        self.generic_visit(node)

    def visit_UnaryOp(self, node):
        # Унарные операции: -, +, ~, not
        op_map = {
            ast.USub: '-', ast.UAdd: '+', ast.Invert: '~', ast.Not: 'not'
        }
        self._add_operator(op_map.get(type(node.op), type(node.op).__name__))
        self.generic_visit(node)

    def visit_Compare(self, node):
        # Операции сравнения: ==, !=, <, <=, >, >=, is, is not, in, not in
        op_map = {
            ast.Eq: '==', ast.NotEq: '!=', ast.Lt: '<', ast.LtE: '<=',
            ast.Gt: '>', ast.GtE: '>=', ast.Is: 'is', ast.IsNot: 'is not',
            ast.In: 'in', ast.NotIn: 'not in'
        }
        for op_node in node.ops: # Сравнения могут быть цепочкой (a < b < c)
            self._add_operator(op_map.get(type(op_node), type(op_node).__name__))
        self.generic_visit(node)

    def visit_BoolOp(self, node):
        # Логические операции: and, or
        op_map = {ast.And: 'and', ast.Or: 'or'}
        self._add_operator(op_map.get(type(node.op), type(node.op).__name__))
        self.generic_visit(node)

    def visit_Assign(self, node):
        # Оператор присваивания =
        # Если у нас составное присваивание (+=, -=), оно будет через AugAssign
        self._add_operator('=')
        self.generic_visit(node)

    def visit_AugAssign(self, node):
        # Составные операторы присваивания: +=, -=, *=, и т.д.
        # node.op это, например, ast.Add, ast.Sub
        op_map = {
            ast.Add: '+=', ast.Sub: '-=', ast.Mult: '*=', ast.Div: '/=',
            ast.FloorDiv: '//=', ast.Mod: '%=', ast.Pow: '**=',
            ast.LShift: '<<=', ast.RShift: '>>=', ast.BitOr: '|=',
            ast.BitXor: '^=', ast.BitAnd: '&=' , ast.MatMult: '@='
        }
        self._add_operator(op_map.get(type(node.op), type(node.op).__name__ + '='))
        self.generic_visit(node)

    def visit_Call(self, node):
        # Вызов функции. Имя функции (node.func) обычно Name или Attribute.
        # Считаем сам факт вызова (скобки) как оператор "()".
        # Имя функции будет посчитано как операнд или оператор в visit_Name/visit_Attribute.
        self._add_operator('()') # Оператор вызова функции
        # Аргументы, ключевые слова-аргументы и *args, **kwargs являются операндами
        # и будут обработаны при обходе их узлов.
        self.generic_visit(node)
        
    def visit_Attribute(self, node):
        # Доступ к атрибуту: object.attribute
        # Считаем точку '.' как оператор.
        # 'object' и 'attribute' будут операндами (если это не ключевые слова)
        self._add_operator('.')
        self.generic_visit(node) # Посещаем value (object) и attr (attribute name as string)
                                  # node.attr - это строка, не узел, поэтому ее нужно добавить как операнд вручную,
                                  # если мы не хотим, чтобы visit_Name ее ловил (если она не является Name узлом).
                                  # Однако, если attr - это просто строка, то наш visit_Name его не поймает.
                                  # Для простоты, если attr - это идентификатор, он будет пойман visit_Name.
                                  # Это требует аккуратной настройки.

    def visit_Subscript(self, node):
        # Индексация или срез: object[index]
        # Считаем '[]' как оператор.
        # 'object' и 'index' будут операндами/операторами в зависимости от их типа.
        self._add_operator('[]')
        self.generic_visit(node)

    # Ключевые слова как операторы
    def visit_Await(self, node): self._add_operator('await'); self.generic_visit(node)
    
    # Для f-строк (Python 3.6+)
    def visit_JoinedStr(self, node):
        # Сама f-строка - это операнд, но ее части могут быть выражениями
        # Мы можем считать f"" как оператор конкатенации строк, или просто обработать внутренности
        # Для простоты, не будем добавлять f-строку как специальный оператор, а позволим
        # generic_visit обойти ее части (FormattedValue, Constant).
        # _add_operand(f"f-string") # Если бы хотели посчитать саму f-строку как один операнд
        self.generic_visit(node)

    def visit_FormattedValue(self, node):
        # Часть f-строки: f"{expr}"
        # Считаем {} как оператор форматирования
        self._add_operator('{}') # или 'f{}'
        self.generic_visit(node)

    # Другие узлы, которые могут быть интересны, но не всегда очевидно, операторы они или нет
    # visit_List, visit_Tuple, visit_Set, visit_Dict - создание коллекций
    # Можно считать [] , () , {} как операторы создания.
    def visit_List(self, node): self._add_operator('list_literal[]'); self.generic_visit(node)
    def visit_Tuple(self, node): self._add_operator('tuple_literal()'); self.generic_visit(node)
    def visit_Set(self, node): self._add_operator('set_literal{}'); self.generic_visit(node)
    def visit_Dict(self, node): self._add_operator('dict_literal{}'); self.generic_visit(node)

    # Срезы
    def visit_Slice(self, node):
        # Например, a[1:2:3]. ':' является оператором.
        # Может быть несколько ':' в сложном срезе.
        # ast.Slice не имеет прямого токена ':', но сам узел Slice можно считать оператором
        # или полагаться на то, что visit_Subscript уже посчитал '[]'.
        # Добавим ':' за каждый не None элемент в slice.
        # if node.lower is not None: self._add_operator(':') (если бы это был простой срез)
        # Для простоты, можно просто добавить оператор "slice"
        self._add_operator('slice_expr') # или ':' если хотим гранулярнее
        self.generic_visit(node)


def get_halstead_metrics_from_ast(code_string):
    if code_string.startswith('\ufeff'): # Убираем BOM на всякий случай
        code_string = code_string[1:]
        
    try:
        tree = ast.parse(code_string)
    except SyntaxError as e:
        sys.stderr.write(f"Python AST Parser SyntaxError: {e.msg} at line {e.lineno}, offset {e.offset}\n")
        sys.stderr.write(f"Problematic line: {e.text}")
        return None, None, 0, 0
    except Exception as e:
        sys.stderr.write(f"Python AST Parser Error: {e}\n")
        return None, None, 0, 0

    visitor = HalsteadMetricsVisitor()
    visitor.visit(tree)
    
    return visitor.operators, visitor.operands, visitor.N1, visitor.N2

if __name__ == "__main__":
    if len(sys.argv) < 2:
        sys.stderr.write("Usage: python parse_python.py <file_path>\n")
        sys.exit(1)

    file_path = sys.argv[1]
    try:
        # Читаем файл, явно указывая UTF-8, но ast.parse должен сам справиться с кодировкой из файла
        with open(file_path, 'r', encoding='utf-8') as f:
            source_code = f.read()
        
        operators, operands, N1, N2 = get_halstead_metrics_from_ast(source_code)

        if operators is None: # Ошибка парсинга была обработана в функции
            sys.exit(1)
            
        # Вывод в формате, который будет парсить C#
        # Сортируем для консистентности вывода (полезно для тестов)
        print(f"operators:{','.join(sorted(list(operators)))}")
        print(f"operands:{','.join(sorted(list(operands)))}")
        print(f"N1:{N1}")
        print(f"N2:{N2}")

    except FileNotFoundError:
        sys.stderr.write(f"Error: Python script could not find file at {file_path}\n")
        sys.exit(1)
    except Exception as e:
        sys.stderr.write(f"An unexpected error occurred in Python script: {e}\n")
        sys.exit(1)