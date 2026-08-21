using System;
using System.Collections.Generic;
using System.Text;
using System.Web;

/// <summary>
/// Browsing and looking after the categories. Anyone can add one; only an
/// administrator can rename or delete, since either affects every film
/// carrying it.
/// </summary>
public partial class TagsPage : PageBase
{
    public string Message { get; private set; }
    public string MessageKind { get; private set; }
    public List<Tag> Tags = new List<Tag>();

    protected void Page_Load(object sender, EventArgs e)
    {
        MessageKind = "error";

        if (String.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            if (!Csrf.IsValid(Request))
                Message = "That form expired. Please try again.";
            else if (Handle())
                return;
        }

        Tags = TagRepository.All();
        rptTags.DataSource = Tags;
        rptTags.DataBind();

        phManage.Visible = CurrentUser.IsAdmin && Tags.Count > 0;
        phMessage.Visible = !String.IsNullOrEmpty(Message);
    }

    /// <summary>Returns true when the response has been redirected.</summary>
    private bool Handle()
    {
        string action = Posted("action");

        if (action == "add")
        {
            List<string> names = TagRepository.SplitNames(Posted("names"));
            if (names.Count == 0)
            {
                Message = "Please give the category a name.";
                return false;
            }

            foreach (string name in names) TagRepository.EnsureTag(name, CurrentUser.UserId);

            SetFlash(names.Count == 1
                         ? "Added the " + names[0] + " category."
                         : "Added " + names.Count + " categories.", "success");
            Go("~/Tags.aspx");
            return true;
        }

        // Renaming and deleting change what everyone sees, so they are the
        // administrator's to do.
        if (!CurrentUser.IsAdmin)
        {
            Message = "Only an administrator can rename or delete a category.";
            return false;
        }

        int tagId = FormInt("tagId");
        if (tagId <= 0) return false;

        if (action == "rename")
        {
            string name = TagRepository.Clean(Posted("name"));
            if (name.Length == 0)
            {
                Message = "A category needs a name.";
                return false;
            }

            TagRepository.Rename(tagId, name);
            SetFlash("Renamed to " + name + ".", "success");
            Go("~/Tags.aspx");
            return true;
        }

        if (action == "delete")
        {
            TagRepository.Delete(tagId);
            SetFlash("Category deleted and taken off every movie that had it.", "success");
            Go("~/Tags.aspx");
            return true;
        }

        return false;
    }

    // ----- markup helpers ---------------------------------------------------

    /// <summary>Every category as a chip linking to the library filtered by it.</summary>
    public string Cloud
    {
        get
        {
            if (Tags.Count == 0)
                return "<span class=\"hint\">No categories yet.</span>";

            StringBuilder sb = new StringBuilder();
            foreach (Tag tag in Tags)
            {
                sb.Append("<a class=\"tag\" href=\"")
                  .Append(Attr("Movies.aspx?show=all&tag=" + Url(tag.TagKey))).Append("\">")
                  .Append(H(tag.TagName))
                  .Append("<span class=\"tag-count\">").Append(tag.MovieCount).Append("</span></a>");
            }
            return sb.ToString();
        }
    }

    protected string RenameForm(Tag tag)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("<form method=\"post\" action=\"\" class=\"row-actions\">");
        sb.Append(Csrf.Field);
        sb.Append("<input type=\"hidden\" name=\"action\" value=\"rename\" />");
        sb.Append("<input type=\"hidden\" name=\"tagId\" value=\"").Append(tag.TagId).Append("\" />");
        sb.Append("<input type=\"text\" name=\"name\" maxlength=\"50\" value=\"")
          .Append(HttpUtility.HtmlAttributeEncode(tag.TagName)).Append("\" />");
        sb.Append("<button type=\"submit\" class=\"btn btn-small\">Save</button>");
        sb.Append("</form>");
        return sb.ToString();
    }

    protected string DeleteForm(Tag tag)
    {
        string confirm = "Delete the " + tag.TagName.Replace("'", "\\'") + " category" +
                         (tag.MovieCount > 0 ? " from " + tag.MovieCount + " movies" : "") + "?";

        StringBuilder sb = new StringBuilder();
        sb.Append("<form method=\"post\" action=\"\" onsubmit=\"return confirm('")
          .Append(HttpUtility.HtmlAttributeEncode(confirm)).Append("');\">");
        sb.Append(Csrf.Field);
        sb.Append("<input type=\"hidden\" name=\"action\" value=\"delete\" />");
        sb.Append("<input type=\"hidden\" name=\"tagId\" value=\"").Append(tag.TagId).Append("\" />");
        sb.Append("<button type=\"submit\" class=\"btn btn-small btn-danger\">Delete</button>");
        sb.Append("</form>");
        return sb.ToString();
    }
}
