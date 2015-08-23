<%@ Page Title="Home Page" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="RedisDemo._Default" %>

<asp:Content runat="server" ID="FeaturedContent" ContentPlaceHolderID="FeaturedContent">
    <section class="featured">
        <div class="content-wrapper">
            <hgroup class="title">
                <h1>Redis Demo</h1>
            </hgroup>
        </div>
    </section>
</asp:Content>
<asp:Content runat="server" ID="BodyContent" ContentPlaceHolderID="MainContent">
    <asp:Button ID="btnOpenDB" runat="server" Text="打开Redis" OnClick="btnOpenDB_Click" />&nbsp;&nbsp;
    <asp:Label ID="lblShow" runat="server" Text=""></asp:Label>
    <br />
    <asp:Button ID="btnSetValue" runat="server" Text="显示全部" OnClick="btnSetValue_Click" />&nbsp;&nbsp;
    <asp:Label ID="lblPeople" runat="server" Text=""></asp:Label>
    <br />
    <asp:Label ID="Label1" runat="server" Text="姓名："></asp:Label><asp:TextBox ID="txtName" runat="server" Width="100px"></asp:TextBox>
    <asp:Label ID="Label2" runat="server" Text="部门："></asp:Label><asp:TextBox ID="txtPosition" runat="server" Width="100px"></asp:TextBox>
    <asp:Button ID="btnInsert" runat="server" Text="写入数据" OnClick="btnInsert_Click" />
    <br />
    <asp:Label ID="Label3" runat="server" Text="ID："></asp:Label><asp:TextBox ID="txtRedisId" runat="server" Width="100px"></asp:TextBox>
    <asp:Button ID="btnDel" runat="server" Text="删除数据" OnClick="btnDel_Click" />
    <br />
    <asp:Label ID="Label4" runat="server" Text="部门："></asp:Label><asp:TextBox ID="txtScreenPosition" runat="server" Width="100px"></asp:TextBox>
    <asp:Button ID="btnSearch" runat="server" Text="查询数据" OnClick="btnSearch_Click" />
</asp:Content>
