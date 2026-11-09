package ntr.best.dotabyssmover;

import android.app.Activity;
import android.content.Intent;
import android.content.pm.PackageInfo;
import android.content.pm.PackageManager;
import android.net.Uri;
import android.os.Bundle;
import android.view.Gravity;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

import java.io.BufferedReader;
import java.io.File;
import java.io.InputStreamReader;
import java.util.Locale;

/**
 * AbyssMover —— dotabyss mod 跨签名线/跨包数据搬家工具。
 *
 * Android 11+ 的 FUSE 按 uid 硬隔离 Android/data，MANAGE_EXTERNAL_STORAGE 也读不到别人的包目录，
 * 所以唯一通道是 root。本工具所有数据操作都走 `su -c`。
 */
public class MainActivity extends Activity {

    static final String MOD_PKG = "jp.co.fanzagames.dotabyss_x_a.mod";
    static final String OFF_PKG = "jp.co.fanzagames.dotabyss_x_a";
    static final String EXT = "/storage/emulated/0";
    static final String BACKUP_ROOT = EXT + "/dotabyss-mover";

    TextView tv;
    LinearLayout root;

    @Override
    protected void onCreate(Bundle b) {
        super.onCreate(b);
        ScrollView sv = new ScrollView(this);
        root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setPadding(40, 60, 40, 60);
        sv.addView(root);
        setContentView(sv);

        tv = new TextView(this);
        tv.setTextSize(13);
        tv.setTypeface(android.graphics.Typeface.MONOSPACE);
        root.addView(tv);

        refresh();
    }

    void log(String s) {
        runOnUiThread(() -> tv.append(s + "\n"));
    }

    // ---------- shell ----------

    static final class R {
        int code;
        String out = "";

        boolean ok() {
            return code == 0;
        }
    }

    static final String SU = "/system/bin/su";

    R sh(String cmd) {
        R r = new R();
        try {
            Process p = new ProcessBuilder(SU, "-c", cmd)
                    .redirectErrorStream(true).start();
            try (BufferedReader br = new BufferedReader(new InputStreamReader(p.getInputStream()))) {
                StringBuilder sb = new StringBuilder();
                String line;
                while ((line = br.readLine()) != null) sb.append(line).append('\n');
                r.out = sb.toString().trim();
            }
            r.code = p.waitFor();
        } catch (Exception e) {
            r.code = -1;
            r.out = e.toString();
        }
        return r;
    }

    boolean hasRoot() {
        R r = sh("id");
        return r.ok() && r.out.contains("uid=0");
    }

    String suExe() {
        for (String p : new String[]{"/system/bin/su", "/debug_ramdisk/su", "/sbin/su", "/system/xbin/su"}) {
            if (new File(p).exists()) return p;
        }
        return "su";
    }

    String pkgDataDir(String pkg) {
        // 内部 /data/data 目录（含 shared_prefs）
        return "/data/data/" + pkg;
    }

    String extDataDir(String pkg) {
        return EXT + "/Android/data/" + pkg;
    }

    // ---------- UI ----------

    void refresh() {
        tv.setText("");
        root.removeViews(1, root.getChildCount() - 1);

        boolean root_ok = hasRoot();
        R probe = sh("/system/bin/su -c id");
        log("[su探测] code=" + probe.code + " out=" + probe.out.replace('\n', ' '));
        log(root_ok ? "[root] 已授权 ✓" : "[root] 不可用 ✗  （本工具必须 root）");
        log("");

        String modExt = extDataDir(MOD_PKG), modInt = pkgDataDir(MOD_PKG);
        String offExt = extDataDir(OFF_PKG), offInt = pkgDataDir(OFF_PKG);

        log("[MOD 包]   " + pkgState(MOD_PKG));
        log("  外部 " + sizeOf(modExt) + "   内部 " + sizeOf(modInt));
        log("[官方包]  " + pkgState(OFF_PKG));
        log("  外部 " + sizeOf(offExt) + "   内部 " + sizeOf(offInt));
        log("[备份区]  " + BACKUP_ROOT);
        log("  " + sizeOf(BACKUP_ROOT));
        log("[剩余]    " + sh("df -h /data | tail -1 | awk '{print $4}'").out);
        log("");

        addBtn("① 备份 MOD 全部数据（卸载前）", this::doBackup);
        addBtn("② 恢复 MOD 全部数据（装好新版后）", this::doRestore);
        addBtn("③ 官方数据 → MOD（免重下资源）", () -> doCopy(OFF_PKG, MOD_PKG));
        addBtn("④ MOD 数据 → 官方（免重下资源）", () -> doCopy(MOD_PKG, OFF_PKG));
        addBtn("⑤ 卸载 MOD（跳系统卸载页）", () -> uninstall(MOD_PKG));
        addBtn("⑥ 卸载官方（跳系统卸载页）", () -> uninstall(OFF_PKG));
        addBtn("⑦ 列出备份内容", () -> log(sh("ls -la " + BACKUP_ROOT + " 2>&1").out));
        addBtn("刷新", this::refresh);

        log("");
        log("旧签名升新版：① → ⑤ → 装 v19 → ②");
        log("官方转 MOD  ：装 v19 → ③（只搬资源，约 7GB 免下载）");
    }

    String pkgState(String pkg) {
        try {
            PackageInfo pi = getPackageManager().getPackageInfo(pkg, 0);
            return "已安装 " + pi.versionName;
        } catch (PackageManager.NameNotFoundException e) {
            return "未安装";
        }
    }

    String sizeOf(String p) {
        String s = sh("du -sh '" + p + "' 2>/dev/null | cut -f1").out;
        return s.isEmpty() ? "-" : s;
    }

    void uninstall(String pkg) {
        startActivity(new Intent(Intent.ACTION_DELETE, Uri.parse("package:" + pkg)));
    }

    void addBtn(String label, Runnable r) {
        Button btn = new Button(this);
        btn.setText(label);
        btn.setAllCaps(false);
        btn.setGravity(Gravity.CENTER_VERTICAL | Gravity.START);
        btn.setOnClickListener(v -> new Thread(() -> {
            log("── " + label);
            try {
                r.run();
            } catch (Exception e) {
                log("!! " + e);
            }
        }).start());
        root.addView(btn, LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
    }

    void toast(String s) {
        runOnUiThread(() -> Toast.makeText(this, s, Toast.LENGTH_LONG).show());
    }

    // ---------- 操作 ----------

    void doBackup() {
        if (!hasRoot()) { toast("需要 root"); return; }
        String bk = BACKUP_ROOT + "/backup_" + MOD_PKG;
        log("目标 " + bk + "（约 7GB，3-10 分钟，别锁屏）");
        log("清旧备份…");
        sh("rm -rf '" + bk + "'");
        sh("mkdir -p '" + bk + "/ext' '" + bk + "/int'");

        log("复制外部数据…");
        R r1 = sh("cp -a '" + extDataDir(MOD_PKG) + "/.' '" + bk + "/ext/' 2>&1");
        if (!r1.ok()) log("  外部复制有问题: " + r1.out);
        log("  外部完成 " + sizeOf(bk + "/ext"));

        log("复制内部数据…");
        R r2 = sh("cp -a '" + pkgDataDir(MOD_PKG) + "/.' '" + bk + "/int/' 2>&1");
        if (!r2.ok()) log("  内部复制有问题: " + r2.out);
        log("  内部完成 " + sizeOf(bk + "/int"));

        sh("chmod -R 0777 '" + bk + "'");
        log("备份完成，总计 " + sizeOf(bk));
        toast("备份完成");
    }

    void doRestore() {
        if (!hasRoot()) { toast("需要 root"); return; }
        String bk = BACKUP_ROOT + "/backup_" + MOD_PKG;
        if (sh("test -d '" + bk + "/ext'").code != 0) { toast("没有备份"); return; }

        String ext = extDataDir(MOD_PKG), in = pkgDataDir(MOD_PKG);
        if (sh("test -d '" + ext + "'").code != 0) { toast("MOD 未安装（外部目录不存在）"); return; }

        log("复制回外部目录…");
        R r1 = sh("cp -a '" + bk + "/ext/.' '" + ext + "/' 2>&1");
        if (!r1.ok()) log("  " + r1.out);
        log("  完成 " + sizeOf(ext));

        if (sh("test -d '" + in + "'").code == 0) {
            String uidgid = sh("stat -c '%u:%g' '" + in + "'").out;
            log("复制回内部目录（属主 " + uidgid + "）…");
            R r2 = sh("cp -a '" + bk + "/int/.' '" + in + "/' 2>&1");
            if (!r2.ok()) log("  " + r2.out);
            sh("chown -R " + uidgid + " '" + in + "'");
            sh("restorecon -R '" + in + "' 2>/dev/null");
            log("  完成");
        }

        String uidgid2 = sh("stat -c '%u:%g' '" + ext + "'").out;
        sh("chown -R " + uidgid2 + " '" + ext + "'");
        log("恢复完成，直接开游戏");
        toast("恢复完成");
    }

    void doCopy(String from, String to) {
        if (!hasRoot()) { toast("需要 root"); return; }
        String se = extDataDir(from), te = extDataDir(to);
        if (sh("test -d '" + se + "'").code != 0) { toast("源未安装或无数据"); return; }
        if (sh("test -d '" + te + "'").code != 0) { toast("目标未安装"); return; }

        String uidgid = sh("stat -c '%u:%g' '" + te + "'").out;
        log("目标 uid:gid = " + uidgid);

        String[] subs = {
                "files/DownloadCache",
                "files/UnityCache",
                "files/com.unity.addressables",
        };
        for (String sub : subs) {
            if (sh("test -d '" + se + "/" + sub + "'").code != 0) continue;
            log("  " + sub + " (" + sizeOf(se + "/" + sub) + ")");
            sh("mkdir -p '" + te + "/" + sub + "'");
            R r = sh("cp -an '" + se + "/" + sub + "/.' '" + te + "/" + sub + "/' 2>&1");
            if (!r.ok()) log("    ! " + r.out);
        }
        sh("chown -R " + uidgid + " '" + te + "'");
        sh("restorecon -R '" + te + "' 2>/dev/null");
        log("完成。资源按内容哈希寻址，跨包通用，无需重新下载。");
        toast("复制完成");
    }
}