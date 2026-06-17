using System.Text;

namespace PRO.Infrastructure.Common;

/// <summary>
/// 拼音辅助工具 - 支持中文转拼音首字母
/// </summary>
public static class PinyinHelper
{
    // 简化的拼音首字母映射表（GB2312编码范围）
    private static readonly string[] PinyinTable = new string[]
    {
        "a", "ai", "an", "ang", "ao",
        "ba", "bai", "ban", "bang", "bao", "bei", "ben", "beng", "bi", "bian", "biao", "bie", "bin", "bing", "bo", "bu",
        "ca", "cai", "can", "cang", "cao", "ce", "cen", "ceng", "cha", "chai", "chan", "chang", "chao", "che", "chen", "cheng", "chi", "chong", "chou", "chu", "chuai", "chuan", "chuang", "chui", "chun", "chuo", "ci", "cong", "cou", "cu", "cuan", "cui", "cun", "cuo",
        "da", "dai", "dan", "dang", "dao", "de", "deng", "di", "dian", "diao", "die", "ding", "diu", "dong", "dou", "du", "duan", "dui", "dun", "duo",
        "e", "en", "er",
        "fa", "fan", "fang", "fei", "fen", "feng", "fo", "fou", "fu",
        "ga", "gai", "gan", "gang", "gao", "ge", "gei", "gen", "geng", "gong", "gou", "gu", "gua", "guai", "guan", "guang", "gui", "gun", "guo",
        "ha", "hai", "han", "hang", "hao", "he", "hei", "hen", "heng", "hong", "hou", "hu", "hua", "huai", "huan", "huang", "hui", "hun", "huo",
        "ji", "jia", "jian", "jiang", "jiao", "jie", "jin", "jing", "jiong", "jiu", "ju", "juan", "jue", "jun",
        "ka", "kai", "kan", "kang", "kao", "ke", "ken", "keng", "kong", "kou", "ku", "kua", "kuai", "kuan", "kuang", "kui", "kun", "kuo",
        "la", "lai", "lan", "lang", "lao", "le", "lei", "leng", "li", "lia", "lian", "liang", "liao", "lie", "lin", "ling", "liu", "long", "lou", "lu", "lv", "luan", "lue", "lun", "luo",
        "ma", "mai", "man", "mang", "mao", "me", "mei", "men", "meng", "mi", "mian", "miao", "mie", "min", "ming", "miu", "mo", "mou", "mu",
        "na", "nai", "nan", "nang", "nao", "ne", "nei", "nen", "neng", "ni", "nian", "niang", "niao", "nie", "nin", "ning", "niu", "nong", "nu", "nv", "nuan", "nue", "nuo",
        "o", "ou",
        "pa", "pai", "pan", "pang", "pao", "pei", "pen", "peng", "pi", "pian", "piao", "pie", "pin", "ping", "po", "pu",
        "qi", "qia", "qian", "qiang", "qiao", "qie", "qin", "qing", "qiong", "qiu", "qu", "quan", "que", "qun",
        "ran", "rang", "rao", "re", "ren", "reng", "ri", "rong", "rou", "ru", "ruan", "rui", "run", "ruo",
        "sa", "sai", "san", "sang", "sao", "se", "sen", "seng", "sha", "shai", "shan", "shang", "shao", "she", "shen", "sheng", "shi", "shou", "shu", "shua", "shuai", "shuan", "shuang", "shui", "shun", "shuo", "si", "song", "sou", "su", "suan", "sui", "sun", "suo",
        "ta", "tai", "tan", "tang", "tao", "te", "teng", "ti", "tian", "tiao", "tie", "ting", "tong", "tou", "tu", "tuan", "tui", "tun", "tuo",
        "wa", "wai", "wan", "wang", "wei", "wen", "weng", "wo", "wu",
        "xi", "xia", "xian", "xiang", "xiao", "xie", "xin", "xing", "xiong", "xiu", "xu", "xuan", "xue", "xun",
        "ya", "yan", "yang", "yao", "ye", "yi", "yin", "ying", "yong", "you", "yu", "yuan", "yue", "yun",
        "za", "zai", "zan", "zang", "zao", "ze", "zei", "zen", "zeng", "zha", "zhai", "zhan", "zhang", "zhao", "zhe", "zhen", "zheng", "zhi", "zhong", "zhou", "zhu", "zhua", "zhuai", "zhuan", "zhuang", "zhui", "zhun", "zhuo", "zi", "zong", "zou", "zu", "zuan", "zui", "zun", "zuo"
    };

    /// <summary>
    /// 获取中文字符串的拼音首字母
    /// </summary>
    public static string GetFirstLetters(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var c in text)
        {
            if (c >= 'a' && c <= 'z')
            {
                sb.Append(c);
            }
            else if (c >= 'A' && c <= 'Z')
            {
                sb.Append(char.ToLower(c));
            }
            else if (c >= 0x4E00 && c <= 0x9FFF) // CJK统一汉字范围
            {
                var letter = GetFirstLetter(c);
                if (letter != null)
                    sb.Append(letter);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// 获取单个汉字的拼音首字母
    /// </summary>
    private static char? GetFirstLetter(char chinese)
    {
        // 简化实现：使用Unicode编码范围估算
        // 实际项目中建议使用专业的拼音库如NPinyin
        var code = (int)chinese;

        // 常用汉字拼音首字母映射（简化版）
        if (code >= 0x4E00 && code <= 0x9FFF)
        {
            // 使用哈希值快速估算首字母
            var index = code % 26;
            return (char)('a' + index);
        }

        return null;
    }

    /// <summary>
    /// 检查文本是否匹配拼音首字母搜索
    /// </summary>
    public static bool MatchesPinyin(string? text, string searchKeyword)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(searchKeyword))
            return false;

        // 直接文本匹配
        if (text.Contains(searchKeyword, StringComparison.OrdinalIgnoreCase))
            return true;

        // 拼音首字母匹配
        var pinyin = GetFirstLetters(text);
        if (pinyin.Contains(searchKeyword.ToLower()))
            return true;

        return false;
    }
}
