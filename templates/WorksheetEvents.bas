Attribute VB_Name = "WorksheetEvents"
Option Explicit

' 将此代码复制到目标工作表的代码窗口，或通过 ThisWorkbook 统一转发
Private Sub Worksheet_Change(ByVal Target As Range)
	Dim r As Range, c As Range
	On Error GoTo QUIT_SUB
	Set r = Intersect(Target, Rows("3:4"))
	If r Is Nothing Then GoTo QUIT_SUB

	Application.EnableEvents = False
	For Each c In r.Cells
		If c.Row = 3 Then
			If Len(c.Value2) > 0 And Not TableTemplate.IsValidFieldName(c.Value2) Then
				c.Interior.Color = RGB(255, 200, 200)
			Else
				c.Interior.Pattern = xlNone
			End If
		ElseIf c.Row = 4 Then
			If Len(c.Value2) > 0 And Not TableTemplate.IsValidType(c.Value2) Then
				c.Interior.Color = RGB(255, 200, 200)
			Else
				c.Interior.Pattern = xlNone
			End If
		End If
	Next c

QUIT_SUB:
	Application.EnableEvents = True
End Sub

