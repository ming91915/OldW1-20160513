﻿Imports Autodesk.Revit.DB
Imports Autodesk.Revit.UI
Imports System.IO

Public Class GlobalSettings


#Region "   ---   Constants"
    Public Const AppName As String = "OldW"
    ''' <summary>
    ''' 基坑中的土体族文件的名称
    ''' </summary>
    ''' <remarks></remarks>
    Public Const FamilyName_Soil As String = "基坑土体"
#End Region



    ''' <summary>
    ''' 监测数据点的Element中，用一个共享参数来存储此测点的监测数据。此共享参数的Guid值。
    ''' </summary>
    ''' <returns>测点元素中表示监测数据的共享参数的Guid值。</returns>
    ''' <remarks>如果要用扩展方法，请加上标签：System.Runtime.CompilerServices.Extension() </remarks>
    Public Shared ReadOnly Property Guid_Monitor As Guid
        Get
            Return New Guid("c3d04d9e-aa78-4328-90c5-cf58167d1f09")
        End Get
    End Property

#Region "   ---   文件路径"

    ''' <summary>
    ''' Application的Dll所对应的路径，也就是“bin”文件夹的目录。
    ''' </summary>
    ''' <remarks>等效于：Dim thisAssemblyPath As String = System.Reflection.Assembly.GetExecutingAssembly().Location</remarks>
    Public Shared Path_Dlls As String = My.Application.Info.DirectoryPath
    ''' <summary>
    ''' Solution文件所在的文件夹
    ''' </summary>
    Public Shared Path_Solution As String = New DirectoryInfo(Path_Dlls).Parent.FullName
    ''' <summary>
    ''' 存放图标的文件夹
    ''' </summary>
    Public Shared Path_icons As String = Path.Combine(Path_Solution, "Resources\icons")
    ''' <summary>
    ''' 存放族的文件夹
    ''' </summary>
    Public Shared Path_family As String = Path.Combine(Path_Solution, "Resources\Family")
    ''' <summary>
    ''' 存放数据文件
    ''' </summary>
    ''' <remarks></remarks>
    Public Shared Path_data As String = Path.Combine(Path_Solution, "Resources\Data")
    ''' <summary>
    ''' 存放不同项目的文件夹
    ''' </summary>
    Public Shared Path_Projects As String = Path.Combine(Path_Solution, "Projects")
    ''' <summary>
    ''' 监测警戒规范的绝对文件路径
    ''' </summary>
    Public Shared Path_WarningValueUsing As String = Path.Combine(GlobalSettings.Path_data, "WarningValueUsing.dat")

#End Region

#Region "   ---   监测模型名称"

    ''' <summary>
    ''' 监测仪器的族名称（也是族文件的名称），同时也作为监测仪器的类型判断
    ''' </summary>
    ''' <remarks>从枚举值返回对应的枚举字符的方法：GlobalSettings.InstrumentationType.沉降测点.ToString</remarks>
    Public Enum InstrumentationType
        其他

        ''' <summary> 比如地下连续墙的水平位移 </summary>
        墙体测斜
        ''' <summary> 比如基坑外地表的垂直位移 </summary>
        地表隆沉
        ''' <summary> 比如基坑中支撑的轴力 </summary>
        支撑轴力
        ''' <summary> 比如基坑中立柱的垂直位移 </summary>
        立柱隆沉
    End Enum


#End Region

End Class
