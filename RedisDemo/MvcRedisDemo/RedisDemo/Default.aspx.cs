using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using DotNetOpenAuth.OpenId.Extensions.AttributeExchange;
using RedisDemo.Model;
using ServiceStack.Redis.Generic;

namespace RedisDemo
{
    public partial class _Default : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {

        }

        protected void btnOpenDB_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("C:\\Users\\Administrator\\Desktop\\RedisDemo\\redis-2.4.5-win32-win64\\64bit\\redis-server.exe");//此处为Redis的存储路径
            lblShow.Text = "Redis已经打开！";

            using (var redisClient = RedisManager.GetClient())
            {
                var user = redisClient.GetTypedClient<User>();

                if (user.GetAll().Count > 0)
                    user.DeleteAll();

                var qiujialong = new User
                {
                    Id = user.GetNextSequence(),
                    Name = "qiujialong",
                    Job = new Job { Position = ".NET" }
                };
                var chenxingxing = new User
                {
                    Id = user.GetNextSequence(),
                    Name = "chenxingxing",
                    Job = new Job { Position = ".NET" }
                };
                var luwei = new User
                {
                    Id = user.GetNextSequence(),
                    Name = "luwei",
                    Job = new Job { Position = ".NET" }
                };
                var zhourui = new User
                {
                    Id = user.GetNextSequence(),
                    Name = "zhourui",
                    Job = new Job { Position = "Java" }
                };

                var userToStore = new List<User> { qiujialong, chenxingxing, luwei, zhourui };
                user.StoreAll(userToStore);

                Thread.Sleep(3000);

                lblShow.Text = "目前共有：" + user.GetAll().Count.ToString() + "人！";
            }
        }

        protected void btnSetValue_Click(object sender, EventArgs e)
        {
            using (var redisClient = RedisManager.GetClient())
            {
                var user = redisClient.GetTypedClient<User>();
                if (user.GetAll().Count > 0)
                {
                    var htmlStr = string.Empty;
                    foreach (var u in user.GetAll())
                    {
                        htmlStr += "<li>ID=" + u.Id + "&nbsp;&nbsp;姓名：" + u.Name + "&nbsp;&nbsp;所在部门：" + u.Job.Position + "</li>";
                    }
                    lblPeople.Text = htmlStr;
                }
                lblShow.Text = "目前共有：" + user.GetAll().Count.ToString() + "人！";
            }
        }

        protected void btnInsert_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(txtName.Text) && !string.IsNullOrEmpty(txtPosition.Text))
            {
                using (var redisClient = RedisManager.GetClient())
                {
                    var user = redisClient.GetTypedClient<User>();

                    var newUser = new User
                    {
                        Id = user.GetNextSequence(),
                        Name = txtName.Text,
                        Job = new Job { Position = txtPosition.Text }
                    };
                    var userList = new List<User> { newUser };
                    user.StoreAll(userList);

                    if (user.GetAll().Count > 0)
                    {
                        var htmlStr = string.Empty;
                        foreach (var u in user.GetAll())
                        {
                            htmlStr += "<li>ID=" + u.Id + "&nbsp;&nbsp;姓名：" + u.Name + "&nbsp;&nbsp;所在部门：" + u.Job.Position + "</li>";
                        }
                        lblPeople.Text = htmlStr;
                    }
                    lblShow.Text = "目前共有：" + user.GetAll().Count.ToString() + "人！";
                }
            }
        }

        protected void btnDel_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(txtRedisId.Text))
            {
                using (var redisClient = RedisManager.GetClient())
                {
                    var user = redisClient.GetTypedClient<User>();
                    user.DeleteById(txtRedisId.Text);

                    if (user.GetAll().Count > 0)
                    {
                        var htmlStr = string.Empty;
                        foreach (var u in user.GetAll())
                        {
                            htmlStr += "<li>ID=" + u.Id + "&nbsp;&nbsp;姓名：" + u.Name + "&nbsp;&nbsp;所在部门：" + u.Job.Position + "</li>";
                        }
                        lblPeople.Text = htmlStr;
                    }
                    lblShow.Text = "目前共有：" + user.GetAll().Count.ToString() + "人！";
                }
            }
        }

        protected void btnSearch_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(txtScreenPosition.Text))
            {
                using (var redisClient = RedisManager.GetClient())
                {
                    var user = redisClient.GetTypedClient<User>();
                    var userList = user.GetAll().Where(x => x.Job.Position.Contains(txtScreenPosition.Text)).ToList();

                    if (userList.Count > 0)
                    {
                        var htmlStr = string.Empty;
                        foreach (var u in userList)
                        {
                            htmlStr += "<li>ID=" + u.Id + "&nbsp;&nbsp;姓名：" + u.Name + "&nbsp;&nbsp;所在部门：" + u.Job.Position + "</li>";
                        }
                        lblPeople.Text = htmlStr;
                    }
                    lblShow.Text = "筛选后共有：" + userList.Count.ToString() + "人！";
                }
            }
        }
    }
}