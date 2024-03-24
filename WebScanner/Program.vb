Imports System.Net.Sockets
Imports System.Net
Imports System.Collections.Concurrent
Imports System.Threading
Imports System.Text.RegularExpressions
Imports System.Data
Imports System.Data.SqlClient

Module Program

#Region "Declarations"
    Dim counter As Integer
    Dim cq As New ConcurrentQueue(Of IPAddress)()
    Dim cq2 As New ConcurrentQueue(Of IPAddress)()
    Dim ip As String
    Dim queueThread As New Thread(AddressOf queueCruncher)
    Dim myPing As New Net.NetworkInformation.Ping()
    Dim sqlconnectionstring As String = "ConnectionString"
#End Region

#Region "Main"
    Sub Main(args As String())
        queueThread.Start()
        Dim ipRanges As New Dictionary(Of String, String)
        ipRanges.Add("192.168.1.1", "192.168.1.1")
        PingScan(ipRanges)
        Console.WriteLine("Main Thread Done")
        Console.ReadLine()
    End Sub
#End Region

#Region "Queue"
    Private Sub queueCruncher()
        While True
            Console.WriteLine("Portscan Queue: " & cq.Count)
            If cq.Count > 0 Then
                Dim liveip As IPAddress
                While cq.TryDequeue(liveip)
                    ScanningProcess(liveip.ToString(), 80, 80, 1)
                End While
            End If
            Console.WriteLine("Scrape Queue: " & cq2.Count)
            If cq2.Count > 0 Then
                Dim liveip2 As IPAddress
                While cq2.TryDequeue(liveip2)
                    HandleScrape(liveip2.ToString())
                End While
            End If
            Thread.Sleep(5000)
        End While
    End Sub
#End Region

#Region "Ping Scan"
    Public Sub PingScan(list As Dictionary(Of String, String))
        Dim a, b, c, d As Integer
        Dim e, f, g, h As Integer
        For Each item In list
            Dim ip1 As String()
            Dim ip2 As String()
            ip1 = item.Key.Split(".")
            ip2 = item.Value.Split(".")
            a = Convert.ToInt32(ip1(0)) : b = Convert.ToInt32(ip1(1)) : c = Convert.ToInt32(ip1(2)) : d = Convert.ToInt32(ip1(3))
            e = Convert.ToInt32(ip2(0)) : f = Convert.ToInt32(ip2(1)) : g = Convert.ToInt32(ip2(2)) : h = Convert.ToInt32(ip2(3))
            While True
                ip = a.ToString() & "." & b.ToString() & "." & c.ToString() & "." & d.ToString()
                myPing = New System.Net.NetworkInformation.Ping()
                AddHandler myPing.PingCompleted, AddressOf PingRequestCompleted
                Try
                    myPing.SendAsync(ip, ip)
                Catch ex As Exception
                    WriteError(ex.GetType().FullName, ex.Message)
                End Try

                If a >= e And b >= f And c >= g And d >= h Then
                    Exit While
                End If
                If d >= 255 Then
                    d = 0
                    If c >= 255 Then
                        c = 0
                        If b >= 255 Then
                            b = 0
                            If a >= 255 Then
                                a = 0
                            Else
                                a += 1
                            End If
                        Else
                            b += 1
                        End If
                    Else
                        c += 1
                    End If
                Else
                    d += 1
                End If
                If counter >= 2550 Then
                    counter = 0
                    Console.WriteLine("Ping Scan Pause")
                    Thread.Sleep(5000)
                Else
                    counter += 1
                End If
            End While
        Next
    End Sub
    Public Sub PingRequestCompleted(ByVal sender As Object, ByVal e As Net.NetworkInformation.PingCompletedEventArgs)
        If e.Reply.Status = NetworkInformation.IPStatus.Success Then
            cq.Enqueue(IPAddress.Parse(e.UserState.ToString()))
            WriteIP(e.UserState.ToString())
            Console.WriteLine("Host: " & e.UserState.ToString())
        End If
    End Sub
#End Region

#Region "Port Scan"
    Private Async Function IsOpen(host As String, port As Integer) As Task(Of PortState)
        Dim Client As New TcpClient()
        Try
            Await Client.ConnectAsync(host, port)
            Return New PortState(host, True, port)
        Catch ex As SocketException
            Return New PortState(host, False, port)
        Catch ex As ObjectDisposedException
            Return New PortState(host, False, port)
        Finally
            Client.Close()
        End Try
    End Function
    Private Async Sub ScanningProcess(host As String, first As Integer, last As Integer, fibers As Integer)
        Try
            For chunkPort = first To last Step fibers
                Dim results = Await Task.WhenAll(
                                From port In Enumerable.Range(chunkPort, fibers)
                                Where port <= last
                                Select IsOpen(host, port))
                For Each result In results
                    If result.IsOpen Then
                        cq2.Enqueue(IPAddress.Parse(result.host))
                        WritePort(result.host, first)
                        Console.WriteLine("Host: " & result.host & " Port: " & first)
                    End If
                Next
            Next
        Catch ex As Exception
            WriteError(ex.GetType().FullName, ex.Message)
        End Try
    End Sub
    Public Class PortState
        Public Property PortNumber As Integer
        Public Property IsOpen As Boolean
        Public Property host As String
        Public Sub New(hostIP As String, open As Boolean, port As Integer)
            Me.IsOpen = open
            Me.PortNumber = port
            Me.host = hostIP
        End Sub
    End Class
#End Region

#Region "Title Scrape"
    Sub HandleScrape(ByVal hostIP As String)
        Try
            Dim sourceMarkup As String = New WebClient().DownloadString("http://" & hostIP & "/")
            Dim parsedMatch As String = ""
            For Each foundMatch As Match In Regex.Matches(sourceMarkup, "<title>.+<\/title>", RegexOptions.Singleline)
                parsedMatch = foundMatch.Value
                parsedMatch = Replace(parsedMatch, "<title>", "")
                parsedMatch = Replace(parsedMatch, "</title>", "")
                parsedMatch = Replace(parsedMatch, "'", "")
                parsedMatch = Replace(parsedMatch, """", "")
            Next
            Console.WriteLine("Host: " & hostIP & " Title: " & parsedMatch)
            WriteTitle(hostIP, parsedMatch)
        Catch ex As System.Net.WebException
            Select Case ex.Message
                Case "The remote server returned an error: (400) Bad Request."
                    WriteTitle(hostIP, "(400) Bad Request")
                Case "The remote server returned an error: (401) Unauthorized."
                    WriteTitle(hostIP, "(401) Unauthorized")
                Case "The remote server returned an error: (403) Forbidden."
                    WriteTitle(hostIP, "(403) Forbidden")
                Case "The remote server returned an error: (404) Not Found."
                    WriteTitle(hostIP, "(404) Not Found")
                Case "The remote server returned an error: (500) Internal Server Error."
                    WriteTitle(hostIP, "(500) Internal Server Error")
                Case Else
                    WriteError(ex.GetType().FullName, ex.Message)
            End Select
        Catch ex As Exception
            WriteError(ex.GetType().FullName, ex.Message)
        End Try
    End Sub
#End Region

#Region "Database"
    Sub WriteIP(hostIP As String)
        'INSERT INTO LiveIPs (IPAddress,PingTime) VALUES (@IPAddress,@PingTime)
        Using conn As New SqlConnection(sqlconnectionstring)
            Using comm As New SqlCommand()
                With comm
                    .Connection = conn
                    .CommandType = CommandType.StoredProcedure
                    .CommandText = "LiveIPs_Insert"
                    .Parameters.AddWithValue("@IPAddress", hostIP)
                    .Parameters.AddWithValue("@PingTime", DateTime.Now)
                End With
                Try
                    conn.Open()
                    comm.ExecuteNonQuery()
                Catch ex As SqlException
                    WriteError(ex.GetType().FullName, ex.Message)
                End Try
            End Using
        End Using
    End Sub
    Sub WritePort(hostIP As String, port As String)
        'UPDATE LiveIPs SET OpenPorts = @OpenPorts WHERE IPAddress = @IPAddress
        Using conn As New SqlConnection(sqlconnectionstring)
            Using comm As New SqlCommand()
                With comm
                    .Connection = conn
                    .CommandType = CommandType.StoredProcedure
                    .CommandText = "LiveIPs_SetOpenPorts"
                    .Parameters.AddWithValue("@IPAddress", hostIP)
                    .Parameters.AddWithValue("@OpenPorts", port)
                End With
                Try
                    conn.Open()
                    comm.ExecuteNonQuery()
                Catch ex As SqlException
                    WriteError(ex.GetType().FullName, ex.Message)
                End Try
            End Using
        End Using
    End Sub
    Sub WriteTitle(hostIP As String, title As String)
        'UPDATE LiveIPs SET Title = @Title WHERE IPAddress = @IPAddress
        Using conn As New SqlConnection(sqlconnectionstring)
            Using comm As New SqlCommand()
                With comm
                    .Connection = conn
                    .CommandType = CommandType.StoredProcedure
                    .CommandText = "LiveIPs_SetTitle"
                    .Parameters.AddWithValue("@IPAddress", hostIP)
                    .Parameters.AddWithValue("@Title", title)
                End With
                Try
                    conn.Open()
                    comm.ExecuteNonQuery()
                Catch ex As SqlException
                    WriteError(ex.GetType().FullName, ex.Message)
                End Try
            End Using
        End Using
    End Sub
    Sub WriteError(fullName As String, Message As String)
        'INSERT INTO Error (FullName,Message,ExceptionTime) VALUES (@FullName,@Message,@ExceptionTime)
        Using conn As New SqlConnection(sqlconnectionstring)
            Using comm As New SqlCommand()
                With comm
                    .Connection = conn
                    .CommandType = CommandType.StoredProcedure
                    .CommandText = "Errors_Insert"
                    .Parameters.AddWithValue("@FullName", fullName)
                    .Parameters.AddWithValue("@Message", Message)
                    .Parameters.AddWithValue("@ExceptionTime", DateTime.Now)
                End With
                Try
                    conn.Open()
                    comm.ExecuteNonQuery()
                Catch ex As SqlException
                    'Write Error Locally
                End Try
            End Using
        End Using
    End Sub
#End Region

End Module