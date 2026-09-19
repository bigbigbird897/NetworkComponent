using System;
using System.IO;
using System.Windows;
using Common.License;
using Microsoft.Win32;

namespace LicenseTool;

/// <summary>主窗口：选择私钥、录入客户机器码/名称，签发 license.lic。</summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>浏览选择 RSA 私钥 XML 文件。</summary>
    private void OnBrowseKey(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "选择 RSA 私钥 XML 文件",
            Filter = "XML 私钥 (*.xml)|*.xml|所有文件 (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
            txtKeyPath.Text = dlg.FileName;
    }

    /// <summary>切换授权类型时启用/禁用天数输入。</summary>
    private void OnTypeChanged(object sender, RoutedEventArgs e)
    {
        // XAML 初始化阶段 rbPermanent 默认选中会触发本事件，此时控件字段可能尚未赋值，需判空
        if (rbTrial == null || txtDays == null) return;
        txtDays.IsEnabled = rbTrial.IsChecked == true;
    }

    /// <summary>生成 license 文件并另存为。</summary>
    private void OnGenerate(object sender, RoutedEventArgs e)
    {
        try
        {
            string keyPath = txtKeyPath.Text.Trim();
            string machine = txtMachine.Text.Trim();
            string customer = txtCustomer.Text.Trim();

            if (string.IsNullOrEmpty(keyPath) || !File.Exists(keyPath))
                throw new Exception("请先选择有效的私钥文件");
            if (string.IsNullOrEmpty(machine))
                throw new Exception("请填写客户机器码");

            string privateKey = File.ReadAllText(keyPath).Trim();
            string license;

            if (rbTrial.IsChecked == true)
            {
                if (!int.TryParse(txtDays.Text.Trim(), out int days) || days <= 0)
                    throw new Exception("试用天数必须是正整数");
                license = LicenseGenerator.GenerateTrial(privateKey, machine, customer, days);
                lblStatus.Text = $"已生成 {days} 天试用 license。";
            }
            else
            {
                license = LicenseGenerator.GeneratePermanent(privateKey, machine, customer);
                lblStatus.Text = "已生成永久 license。";
            }

            var sfd = new SaveFileDialog
            {
                Title = "保存 license.lic",
                FileName = "license.lic",
                Filter = "License 文件|*.lic"
            };
            if (sfd.ShowDialog() == true)
            {
                File.WriteAllText(sfd.FileName, license);
                lblStatus.Text = $"✔ 已保存：{sfd.FileName}\n把该文件放到客户程序运行目录（与 NetworkComponent.dll 同级）即可。";
            }
        }
        catch (Exception ex)
        {
            lblStatus.Text = "✘ " + ex.Message;
        }
    }
}
