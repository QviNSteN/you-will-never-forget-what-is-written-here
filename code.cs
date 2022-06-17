using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace M13.InterviewProject.Controllers
{
    /// <summary>
    /// sample usage:
    /// 1) check xpath rule '//ol' for site yandex.ru: http://localhost:56660/api/rules/add?site=yandex.ru&rule=%2f%2fol
    /// 2) check rule is saved:  http://localhost:56660/api/rules/get?site=yandex.ru
    /// 3) view text parsed by rule: http://localhost:56660/api/rules/test?page=yandex.ru
    /// 4) view errors in text: http://localhost:56660/api/spell/errors?page=yandex.ru
    /// 5) view errors count in text: http://localhost:56660/api/spell/errorscount?page=yandex.ru
    /// </summary>
    [Route("api")]
    public class ValuesController : Controller
    {
        private readonly HttpClientFactory _clientFactory;
        public static Dictionary<string, string> Rules = new Dictionary<string, string>();

        public ValuesController(HttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        [HttpGet("rules/add")]
        public void Add(string site, string rule)
        {
            lock (Rules)
            {
                if (Rules.ContainsKey(site))
                {
                    Rules.Remove(site);
                }

                Rules.Add(site, rule);
            }
        }

        [HttpGet("rules/get")]
        public object Get(string site)
        {
            string s;
            try
            {
                s = Rules[site];
            }
            catch (Exception exception)
            {
                return NotFound();
            }

            return s;
        }

        [HttpGet("rules/test")]
        public string Test(string page, string rule = null)
        {
            var site = new Uri("http://" + page).Host;

            var text = _clientFactory.CreateClient().GetAsync("http://" + page)
                .ContinueWith(t =>
                {
                    var document = new HtmlDocument();
                    document.LoadHtml(t.Result.Content.ReadAsStringAsync().Result);
                    string innerText = "";
                    foreach (var node in document.DocumentNode.SelectNodes(rule ?? Rules[site]))
                    {
                        innerText = innerText + "\r\n" + node.InnerText;
                    }
                    return innerText;
                }).Result;
            return text;
        }

        [HttpGet("rules/delete")]
        public void Delete(string site)
        {
            try
            {
                Rules.Remove(site);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Проверить текст страницы по заданному адресу и получить список слов с ошибками
        /// </summary>
        [HttpGet("spell/errors")]
        public IEnumerable<string> SpellErrors(string page)
        {
            var site = new Uri("http://" + page).Host;

            var text = _clientFactory.CreateClient().GetAsync("http://" + page)
                .ContinueWith(t =>
                {
                    var document = new HtmlDocument();
                    document.LoadHtml(t.Result.Content.ReadAsStringAsync().Result);
                    var xpath = Rules[site];
                    string innerText = "";
                    foreach (var node in document.DocumentNode.SelectNodes(xpath))
                    {
                        innerText = innerText + "\r\n" + node.InnerText;
                    }
                    return innerText;
                }).Result;

            var textErrors = new List<string>(100);

            new SpellChecker().GetErrors(text).ContinueWith(r =>
            {
                for (int i = 0; i < r.Result.Length; i++)
                {
                    textErrors.Add(r.Result[i].Word);
                }
            });

            return textErrors;
        }

        /// <summary>
        /// Проверить текст страницы по заданному адресу и получить количество слов с ошибками
        /// </summary>
        [HttpGet("spell/errorscount")]
        public int SpellErrorsCount(string page)
        {
            var site = new Uri("http://" + page).Host;

            var text = _clientFactory.CreateClient().GetAsync("http://" + page)
                .ContinueWith(t =>
                {
                    var document = new HtmlDocument();
                    document.LoadHtml(t.Result.Content.ReadAsStringAsync().Result);
                    var xpath = Rules[site];
                    string innerText = "";
                    foreach (var node in document.DocumentNode.SelectNodes(xpath))
                    {
                        innerText = innerText + "\r\n" + node.InnerText;
                    }
                    return innerText;
                }).Result;

            return new SpellChecker().GetErrors(text).Result.Count();
        }
    }

    public class SpellChecker
    {
        public async Task<ISpellCheckError[]> GetErrors(string text)
        {
            //используем сервис яндекса для поиска орфографических ошибок в тексте
            //сервис возвращает список слов, в которых допущены ошибки
            Task<HttpResponseMessage> task = new HttpClient()
                .GetAsync("http://speller.yandex.net/services/spellservice.json/checkText?text=" + WebUtility.UrlEncode(text));

            var errors = task.ContinueWith(t =>
            {
                int count = 0;
                var json = t.Result.Content.ReadAsStringAsync().Result;
                var errs = JsonConvert.DeserializeObject<SpellerErrors[]>(json);
                List<ISpellCheckError> list = new List<ISpellCheckError>(100);
                for (int i = 0; i < errs.Length; i++)
                {
                    list.Add(errs[i] as ISpellCheckError);
                }
                return list;
            }).Result;

            return errors.ToArray();
        }
    }

    public interface ISpellCheckError
    {
        string Word { get; }
    }

    public struct SpellerErrors : ISpellCheckError
    {
        public int Code { get; set; }
        public int Pos { get; set; }
        public int row { get; set; }
        public int col { get; set; }
        public int len { get; set; }
        public string Word { get; set; }
        public string[] s { get; set; }
    }

    public class HttpClientFactory
    {
        public HttpClient CreateClient()
        {
            return
                new HttpClient(new HttpClientHandler
                {
                    AllowAutoRedirect = true,
                    AutomaticDecompression = DecompressionMethods.Deflate
                });
        }
    }
}
