# Scripts/parse_python.py
import sys
import ast
import keyword

# Ключевые слова, которые однозначно считаются операторами
PYTHON_KEYWORD_OPERATORS = {
    'if', 'else', 'elif', 'for', 'while', 'try', 'except', 'finally', 'with', 'as',
    'def', 'class', 'return', 'yield', 'lambda', 'import', 'from', 'pass',
    'break', 'continue', 'global', 'nonlocal', 'assert', 'del', 'raise',
    'and', 'or', 'not', 'is', 'in', # 'is not' и 'not in' - узлы Compare с соответствующими типами
    'async', 'await', 'yield from'
}

class HalsteadMetricsVisitor(ast.NodeVisitor):
    def __init__(self):
        self.operators = set()
        self.operands = set()
        self.N1 = 0  # Total operators count
        self.N2 = 0  # Total operands count

    def _add_operator(self, op_name):
        self.operators.add(op_name)
        self.N1 += 1

    def _add_operand(self, operand_value_or_node):
        name_str = ""
        if isinstance(operand_value_or_node, ast.Name):
            name_str = operand_value_or_node.id
        elif isinstance(operand_value_or_node, ast.Constant): # Python 3.8+
            name_str = str(operand_value_or_node.value) # У Constant значение в .value
        elif isinstance(operand_value_or_node, ast.Constant): # Python < 3.8
            name_str = str(getattr(operand_value_or_node, 'n', getattr(operand_value_or_node, 's', None)))
        elif isinstance(operand_value_or_node, ast.Constant): # Python < 3.8 for True, False, None
             name_str = str(operand_value_or_node.value)
        else: # Предполагаем, что это уже строка (например, для node.attr из Attribute)
            name_str = str(operand_value_or_node)

        # Не добавляем ключевые слова-операторы в операнды
        if name_str not in PYTHON_KEYWORD_OPERATORS:
            self.operands.add(name_str)
            self.N2 += 1

    # --- Обработка идентификаторов и литералов (операнды) ---
    def visit_Name(self, node):
        # Имена (переменные, функции, классы и т.д., когда они используются, а не объявляются)
        # Если имя - это ключевое слово-оператор, оно будет добавлено через свой visit_ метод
        if node.id not in PYTHON_KEYWORD_OPERATORS:
             # True, False, None обрабатываются как константы
            if node.id not in ('True', 'False', 'None'):
                self._add_operand(node) # Передаем узел Name
        self.generic_visit(node) # Посещаем дочерние узлы, если есть

    def visit_Constant(self, node): # Python 3.8+ (числа, строки, True, False, None, Ellipsis)
        self._add_operand(node) # Передаем узел Constant
        self.generic_visit(node)

    # Для совместимости с Python < 3.8 (если ast.Constant не покрывает)
    def visit_Num(self, node): self._add_operand(node); self.generic_visit(node)
    def visit_Str(self, node): self._add_operand(node); self.generic_visit(node)
    def visit_Bytes(self, node): self._add_operand(node); self.generic_visit(node)
    def visit_NameConstant(self, node): self._add_operand(node); self.generic_visit(node) # True, False, None

    # --- Обработка операторов ---
    _bin_op_map = { ast.Add: '+', ast.Sub: '-', ast.Mult: '*', ast.Div: '/', ast.FloorDiv: '//', ast.Mod: '%', ast.Pow: '**', ast.LShift: '<<', ast.RShift: '>>', ast.BitOr: '|', ast.BitXor: '^', ast.BitAnd: '&', ast.MatMult: '@'}
    def visit_BinOp(self, node):
        self._add_operator(self._bin_op_map.get(type(node.op), type(node.op).__name__))
        self.generic_visit(node)

    _unary_op_map = { ast.USub: 'u-', ast.UAdd: 'u+', ast.Invert: '~', ast.Not: 'not_op'} # not_op, т.к. not - ключевое слово
    def visit_UnaryOp(self, node):
        op_name = self._unary_op_map.get(type(node.op), type(node.op).__name__)
        # 'not' как ключевое слово уже есть в PYTHON_KEYWORD_OPERATORS
        # Здесь мы ловим именно узел UnaryOp с операцией Not
        if type(node.op) is ast.Not and 'not' in PYTHON_KEYWORD_OPERATORS:
            # Уже будет посчитано через visit_Name или при обходе ключевых слов, если бы мы делали это отдельно.
            # Для надежности, считаем здесь, т.к. 'not' - явный унарный оператор.
             self._add_operator('not') # или 'not_unary' для различения
        else:
            self._add_operator(op_name)
        self.generic_visit(node)

    _compare_op_map = { ast.Eq: '==', ast.NotEq: '!=', ast.Lt: '<', ast.LtE: '<=', ast.Gt: '>', ast.GtE: '>=', ast.Is: 'is', ast.IsNot: 'is not', ast.In: 'in', ast.NotIn: 'not in'}
    def visit_Compare(self, node):
        # В выражении a < b < c будет несколько операторов в node.ops
        for op_node in node.ops:
            op_name = self._compare_op_map.get(type(op_node), type(op_node).__name__)
            self._add_operator(op_name)
        self.generic_visit(node)

    _bool_op_map = {ast.And: 'and', ast.Or: 'or'}
    def visit_BoolOp(self, node): # and, or
        # Эти ключевые слова также есть в PYTHON_KEYWORD_OPERATORS.
        # Их обработка здесь гарантирует, что они считаются операторами.
        self._add_operator(self._bool_op_map.get(type(node.op), type(node.op).__name__))
        self.generic_visit(node)

    def visit_Assign(self, node): self._add_operator('='); self.generic_visit(node)

    _aug_assign_map = { ast.Add: '+=', ast.Sub: '-=', ast.Mult: '*=', ast.Div: '/=', ast.FloorDiv: '//=', ast.Mod: '%=', ast.Pow: '**=', ast.LShift: '<<=', ast.RShift: '>>=', ast.BitOr: '|=', ast.BitXor: '^=', ast.BitAnd: '&=' , ast.MatMult: '@='}
    def visit_AugAssign(self, node):
        self._add_operator(self._aug_assign_map.get(type(node.op), type(node.op).__name__ + '='))
        self.generic_visit(node)

    def visit_Call(self, node):
        self._add_operator('()') # Оператор вызова функции/метода
        # Имя функции (node.func) и аргументы (node.args, node.keywords) будут обработаны через generic_visit
        self.generic_visit(node)
        
    def visit_Attribute(self, node): # object.attribute
        self._add_operator('.') # Оператор доступа к атрибуту
        # node.value (то, что слева от точки) будет обработано generic_visit
        # node.attr (имя атрибута справа, строка) добавляем как операнд
        self._add_operand(node.attr)
        self.generic_visit(node.value) # Обходим только левую часть, т.к. attr - строка

    def visit_Subscript(self, node): # object[index]
        self._add_operator('[]') # Оператор индексации/среза
        self.generic_visit(node)

    # --- Ключевые слова-операторы (обрабатываем узлы AST, соответствующие этим словам) ---
    def visit_If(self, node): self._add_operator('if'); self.generic_visit(node)
    # 'else' является частью структуры If (node.orelse), но само слово 'else' важно
    # Если orelse не пусто, значит, есть 'else' или 'elif'
    # ast не имеет отдельного узла для 'else' или 'elif' вне If.
    # Если node.orelse есть и это не просто один узел If (для elif), то это 'else'.
    # Мы уже добавили 'if'. Если есть orelse, и он не пуст, это подразумевает 'else' или 'elif'.
    # 'elif' - это просто вложенный If в orelse.
    # Для простоты, 'if' уже посчитан. Наличие orelse можно считать сигналом 'else'.
    # Но чтобы не считать дважды для elif, оставим только 'if'.
    # Альтернатива: в visit_If проверять node.orelse и если непусто и не является ast.If, добавлять 'else'.

    def visit_For(self, node): self._add_operator('for'); self.generic_visit(node)
    def visit_AsyncFor(self, node): self._add_operator('async for'); self.generic_visit(node) # async for
    def visit_While(self, node): self._add_operator('while'); self.generic_visit(node)
    def visit_Try(self, node): self._add_operator('try'); self.generic_visit(node)
    # 'finally' и 'except' являются частями Try
    def visit_ExceptHandler(self, node): self._add_operator('except'); self.generic_visit(node) # Блок except
    # Узел Try имеет finalbody для finally

    def visit_With(self, node): self._add_operator('with'); self.generic_visit(node)
    def visit_AsyncWith(self, node): self._add_operator('async with'); self.generic_visit(node) # async with

    def visit_FunctionDef(self, node): self._add_operator('def'); self.generic_visit(node)
    def visit_AsyncFunctionDef(self, node): self._add_operator('async def'); self.generic_visit(node)
    def visit_ClassDef(self, node): self._add_operator('class'); self.generic_visit(node)

    def visit_Return(self, node): self._add_operator('return'); self.generic_visit(node)
    def visit_Yield(self, node): self._add_operator('yield'); self.generic_visit(node)
    def visit_YieldFrom(self, node): self._add_operator('yield from'); self.generic_visit(node)
    def visit_Lambda(self, node): self._add_operator('lambda'); self.generic_visit(node)

    def visit_Import(self, node): self._add_operator('import'); self.generic_visit(node)
    def visit_ImportFrom(self, node): self._add_operator('from'); self.generic_visit(node) # Также содержит 'import'

    def visit_Pass(self, node): self._add_operator('pass'); self.generic_visit(node)
    def visit_Break(self, node): self._add_operator('break'); self.generic_visit(node)
    def visit_Continue(self, node): self._add_operator('continue'); self.generic_visit(node)

    def visit_Global(self, node): self._add_operator('global'); self.generic_visit(node)
    def visit_Nonlocal(self, node): self._add_operator('nonlocal'); self.generic_visit(node)
    def visit_Assert(self, node): self._add_operator('assert'); self.generic_visit(node)
    def visit_Delete(self, node): self._add_operator('del'); self.generic_visit(node)
    def visit_Raise(self, node): self._add_operator('raise'); self.generic_visit(node)
    def visit_Await(self, node): self._add_operator('await'); self.generic_visit(node)

    # --- Структурные операторы (создание коллекций, срезы, f-строки) ---
    def visit_List(self, node): self._add_operator('list_literal[]'); self.generic_visit(node)
    def visit_Tuple(self, node):
        # Запятые в кортежах - тоже операторы.
        # Если кортеж не пустой и содержит больше одного элемента, добавляем оператор ","
        # Сам факт создания кортежа можно считать оператором 'tuple_literal()'
        self._add_operator('tuple_literal()')
        if len(node.elts) > 1:
            self._add_operator(',') # Запятая как разделитель
            self.N1 += (len(node.elts) - 2) # Если 2 элемента - 1 запятая (уже добавили), 3 эл - 2 запятых (добавляем еще N-2)
        self.generic_visit(node)

    def visit_Set(self, node): self._add_operator('set_literal{}'); self.generic_visit(node)
    def visit_Dict(self, node):
        self._add_operator('dict_literal{}')
        # Двоеточия в словарях (key: value) - операторы
        if node.keys: # Если есть ключи (и, соответственно, значения)
            self._add_operator(':') # Оператор "ключ-значение"
            self.N1 += (len(node.keys) - 1) # Если N ключей, то N двоеточий. Одно уже добавили.
        self.generic_visit(node)
    
    def visit_Slice(self, node): # Внутри Subscript, например, a[1:2], a[:], a[1:2:3]
        # Сам узел Slice представляет собой структуру среза.
        # Двоеточие ':' является оператором.
        self._add_operator(':')
        # ast.Slice не имеет информации о количестве двоеточий, только lower, upper, step.
        # Если есть upper (даже если lower None), значит, есть как минимум одно ':'.
        # Если есть step (даже если lower/upper None), значит, есть как минимум одно (а то и два) ':'.
        # Для простоты считаем один уникальный оператор ':' для срезов.
        self.generic_visit(node)

    def visit_FormattedValue(self, node): # Часть f-строки: f"{expr}"
        self._add_operator('f{}') # Оператор форматирования в f-строке
        self.generic_visit(node)
    # JoinedStr (сама f-строка) не считается оператором, ее части (Constant, FormattedValue) обходятся.

    # --- Генераторы и comprehensions ---
    def visit_ListComp(self, node): self._add_operator('comp_list[]') ; self.generic_visit(node) # [x for x in ...]
    def visit_SetComp(self, node): self._add_operator('comp_set{}')   ; self.generic_visit(node) # {x for x in ...}
    def visit_DictComp(self, node): self._add_operator('comp_dict{:}'); self.generic_visit(node) # {k:v for k,v in ...}
    def visit_GeneratorExp(self, node): self._add_operator('comp_gen()'); self.generic_visit(node) # (x for x in ...)
    # Внутри comprehensions есть 'for' и 'if', которые будут обработаны соответствующими visit_ методами.

    def visit_IfExp(self, node): # Тернарный оператор: value_if_true if condition else value_if_false
        self._add_operator('if_expr'); # Можно считать 'if' и 'else' отдельно, если нужно
        self._add_operator('else_expr');
        self.generic_visit(node)

    # Можно добавить visit_Starred для *args, **kwargs, если считать * и ** операторами распаковки.
    def visit_Starred(self, node): # *args, *[1,2,3], **kwargs
        # Контекст важен: в вызовах, присваиваниях и т.д.
        # Можно считать '*' или '**' операторами здесь.
        # Если node.value это Name, то это *имя_переменной
        # Если в вызове, то это *args или **kwargs
        # Для простоты, пока не будем выделять отдельный оператор для *, если он не арифметический.
        # Он может быть частью синтаксиса аргументов функций.
        # Если же считать его здесь, то нужно определить, что это: * или **.
        # ast.Starred не дает информации о количестве звездочек.
        self._add_operator('*_unpack') # Общий оператор распаковки/упаковки
        self.generic_visit(node)


def get_halstead_metrics_from_ast(code_string):
    # Убираем BOM, если он есть (часто проблема при чтении файлов из Windows)
    if code_string.startswith('\ufeff'):
        code_string = code_string[1:]
        
    try:
        tree = ast.parse(code_string)
    except SyntaxError as e:
        # Формируем более информативное сообщение об ошибке
        error_line = e.text.strip() if e.text else "N/A"
        sys.stderr.write(f"Python AST Parser SyntaxError: {e.msg}\n")
        sys.stderr.write(f"  File \"<string>\", line {e.lineno}, offset {e.offset}\n")
        sys.stderr.write(f"    {error_line}\n")
        if e.offset is not None:
             sys.stderr.write(f"    {' ' * e.offset}^\n")
        return None, None, 0, 0
    except Exception as e: # Другие возможные ошибки при парсинге
        sys.stderr.write(f"Python AST Parser Error: {type(e).__name__} - {e}\n")
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
        # Читаем файл, явно указывая UTF-8, чтобы избежать проблем с кодировкой по умолчанию
        with open(file_path, 'r', encoding='utf-8') as f:
            source_code = f.read()
        
        operators, operands, N1, N2 = get_halstead_metrics_from_ast(source_code)

        if operators is None: # Ошибка парсинга была обработана и выведена в stderr в get_halstead_metrics_from_ast
            sys.exit(1)
            
        # Вывод в формате, который будет парсить C#
        # Сортируем для консистентности вывода (полезно для тестов и сравнения)
        print(f"operators:{','.join(sorted(list(operators)))}")
        print(f"operands:{','.join(sorted(list(operands)))}")
        print(f"N1:{N1}")
        print(f"N2:{N2}")

    except FileNotFoundError:
        sys.stderr.write(f"Error: Python script could not find file at '{file_path}'\n")
        sys.exit(1)
    except Exception as e: # Другие ошибки, например, проблемы с правами доступа к файлу
        sys.stderr.write(f"An unexpected error occurred in Python script: {type(e).__name__} - {e}\n")
        sys.exit(1)