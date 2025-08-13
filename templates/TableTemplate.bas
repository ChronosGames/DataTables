Attribute VB_Name = "TableTemplate"
Option Explicit

' 正则：字段名
Public Function IsValidFieldName(ByVal s As String) As Boolean
	Dim re As Object
	Set re = CreateObject("VBScript.RegExp")
	re.Pattern = "^[A-Za-z][A-Za-z0-9_]*$"
	re.IgnoreCase = False
	re.Global = False
	IsValidFieldName = re.Test(Trim$(s))
End Function

' 去空白并小写
Private Function norm(ByVal s As String) As String
	norm = LCase$(Trim$(s))
End Function

' 去掉外层尖括号 <...>，返回内部，若不匹配返回空串
Private Function innerOf(ByVal s As String) As String
	Dim i As Long, j As Long
	i = InStr(1, s, "<", vbTextCompare)
	j = InStrRev(s, ">")
	If i > 0 And j > i Then
		innerOf = Mid$(s, i + 1, j - i - 1)
	Else
		innerOf = ""
	End If
End Function

' 拆分 Map<K,V> 的 K,V（只切第一次逗号）
Private Function splitMapKV(ByVal s As String, ByRef k As String, ByRef v As String) As Boolean
	Dim lvl As Long, i As Long, ch As String
	For i = 1 To Len(s)
		ch = Mid$(s, i, 1)
		If ch = "<" Then lvl = lvl + 1 _
		ElseIf ch = ">" Then lvl = lvl - 1 _
		ElseIf ch = "," And lvl = 0 Then
			k = Trim$(Left$(s, i - 1))
			v = Trim$(Mid$(s, i + 1))
			splitMapKV = (Len(k) > 0 And Len(v) > 0)
			Exit Function
		End If
	Next
	splitMapKV = False
End Function

' 递归校验类型字符串（与生成器保持一致的允许集合）
Public Function IsValidType(ByVal t As String) As Boolean
	Dim n As String: n = norm(t)
	If n = "" Then IsValidType = False: Exit Function

	' 基础类型
	If InStr(1, "|int|long|float|double|bool|string|byte|sbyte|short|ushort|uint|ulong|decimal|char|datetime|json|", "|" & n & "|") > 0 Then
		IsValidType = True: Exit Function
	End If

	' Enum<T>
	If Left$(n, 5) = "enum<" And Right$(n, 1) = ">" Then
		IsValidType = (Len(innerOf(n)) > 0)
		Exit Function
	End If

	' Array<T>
	If Left$(n, 6) = "array<" And Right$(n, 1) = ">" Then
		IsValidType = IsValidType(innerOf(n))
		Exit Function
	End If

	' Map<K,V>
	If Left$(n, 4) = "map<" And Right$(n, 1) = ">" Then
		Dim kv As String, k As String, v As String
		kv = innerOf(n)
		If splitMapKV(kv, k, v) Then
			IsValidType = IsValidType(k) And IsValidType(v)
		Else
			IsValidType = False
		End If
		Exit Function
	End If

	' JSON<T>
	If Left$(n, 5) = "json<" And Right$(n, 1) = ">" Then
		IsValidType = (Len(innerOf(n)) > 0)
		Exit Function
	End If

	' Custom<...>
	If Left$(n, 7) = "custom<" And Right$(n, 1) = ">" Then
		IsValidType = (Len(innerOf(n)) > 0)
		Exit Function
	End If

	IsValidType = False
End Function

' 根据第4行类型，对第5行及以下按列应用数据验证
Public Sub ApplyColumnValidation()
	Dim ws As Worksheet: Set ws = ActiveSheet
	Dim lastCol As Long: lastCol = ws.Cells(3, ws.Columns.Count).End(xlToLeft).Column ' 以第3行作为列终点
	Dim lastRow As Long: lastRow = ws.Cells(ws.Rows.Count, 1).End(xlUp).Row
	If lastRow < 5 Then lastRow = 5000 ' 预留

	Dim c As Long, t As String, rng As Range
	For c = 1 To lastCol
		t = Trim$(ws.Cells(4, c).Value2)
		Set rng = ws.Range(ws.Cells(5, c), ws.Cells(lastRow, c))
		On Error Resume Next
		rng.Validation.Delete
		On Error GoTo 0

		If t = "" Then GoTo CONTINUE_FOR

		If LCase$(t) = "bool" Then
			rng.Validation.Add Type:=xlValidateList, AlertStyle:=xlValidAlertStop, Operator:=xlBetween, Formula1:="TRUE,FALSE"
		ElseIf InStr(1, "|int|long|byte|sbyte|short|ushort|uint|ulong|", "|" & LCase$(t) & "|") > 0 Then
			rng.Validation.Add Type:=xlValidateWholeNumber, AlertStyle:=xlValidAlertStop, Operator:=xlBetween, Formula1:="-2147483648", Formula2:="2147483647"
		ElseIf InStr(1, "|float|double|decimal|", "|" & LCase$(t) & "|") > 0 Then
			rng.Validation.Add Type:=xlValidateDecimal, AlertStyle:=xlValidAlertStop, Operator:=xlBetween, Formula1:="-1E+308", Formula2:="1E+308"
		ElseIf LCase$(Left$(t, 5)) = "enum<" Then
			' 需要准备命名区域：Enum_类型名（例：Enum_SceneType）
			Dim enumName As String: enumName = Mid$(t, 6, Len(t) - 6)
			On Error Resume Next
			rng.Validation.Add Type:=xlValidateList, AlertStyle:=xlValidAlertStop, _
				Operator:=xlBetween, Formula1:="=Enum_" & enumName
			If Err.Number <> 0 Then
				' 找不到命名区域，则退化为输入提示
				Err.Clear
				rng.Validation.Add Type:=xlValidateInputOnly
				rng.Validation.InputMessage = "请按枚举 " & enumName & " 的取值填写"
			End If
			On Error GoTo 0
		ElseIf Left$(LCase$(t), 6) = "array<" Then
			' 软限制：建议以 [ 开头
			rng.FormatConditions.Delete
			rng.FormatConditions.Add Type:=xlExpression, Formula1:="=LEFT(" & ws.Cells(5, c).Address(False, False) & ",1)<>""["""
			rng.FormatConditions(rng.FormatConditions.Count).Interior.Color = RGB(255, 235, 235)
		ElseIf Left$(LCase$(t), 4) = "map<" Or LCase$(t) = "json" Or Left$(LCase$(t), 5) = "json<" Then
			' 软限制：建议以 { 或 [ 开头
			rng.FormatConditions.Delete
			rng.FormatConditions.Add Type:=xlExpression, Formula1:="=AND(LEFT(" & ws.Cells(5, c).Address(False, False) & ",1)<>""{""", LEFT(" & ws.Cells(5, c).Address(False, False) & ",1)<>""[""")"
			rng.FormatConditions(rng.FormatConditions.Count).Interior.Color = RGB(255, 235, 235)
		End If
CONTINUE_FOR:
	Next c
End Sub

